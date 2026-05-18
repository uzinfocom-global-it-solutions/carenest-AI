using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundServices;

public sealed class NotificationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVoiceActionQueue _queue;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(
        IServiceScopeFactory scopeFactory,
        IVoiceActionQueue queue,
        ILogger<NotificationDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationDispatcherService started");

        await foreach (var action in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await DispatchAsync(action, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Dispatch failed for VoiceAction {Id}", action.Id);
            }
        }
    }

    private async Task DispatchAsync(Domain.Entities.VoiceAction action, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var voiceActionService = scope.ServiceProvider.GetRequiredService<IVoiceActionService>();
        var sse = scope.ServiceProvider.GetRequiredService<ISseConnectionManager>();

        var data = new Dictionary<string, string>
        {
            ["type"]                 = "voice_action",
            ["actionId"]             = action.Id.ToString(),
            ["actionType"]           = action.Type.ToString(),
            ["priority"]             = action.Priority.ToString(),
            ["requiresConfirmation"] = action.RequiresConfirmation.ToString().ToLowerInvariant(),
            ["escalationEnabled"]    = action.EscalationEnabled.ToString().ToLowerInvariant(),
            ["text"]                 = action.Text,
        };

        if (action.Metadata is not null) data["metadata"] = action.Metadata;

        // Prefer explicit title from metadata (set by test mode and orchestrator)
        string? metaTitle = null;
        if (action.Metadata is not null)
        {
            try
            {
                var meta = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(action.Metadata);
                if (meta.TryGetProperty("title", out var t)) metaTitle = t.GetString();
            }
            catch { /* ignore bad metadata */ }
        }

        var title = metaTitle ?? action.Priority switch
        {
            NotificationPriority.Emergency => "🚨 Экстренное уведомление",
            NotificationPriority.High      => action.Type switch
            {
                VoiceActionType.EmergencyAlert     => "🆘 Требуется помощь",
                VoiceActionType.MedicationReminder => "💊 Лекарство — срочно",
                _                                  => "⚠️ Важное уведомление",
            },
            _ => action.Type switch
            {
                VoiceActionType.MedicationReminder => "💊 Напоминание о лекарстве",
                VoiceActionType.FollowUpCheck      => "🔍 Проверка самочувствия",
                VoiceActionType.CalendarAlert      => "📅 Напоминание",
                _                                  => "🤖 CareNestAI",
            },
        };

        // Send FCM push
        await push.SendToUserAsync(
            action.UserId, title, action.Text, action.Priority, data, ct);

        // Publish SSE realtime event
        await sse.PublishAsync(action.UserId, new SseEvent(
            "voice_action_dispatch",
            JsonSerializer.Serialize(new
            {
                actionId = action.Id,
                type = action.Type.ToString(),
                priority = action.Priority.ToString(),
                text = action.Text,
                requiresConfirmation = action.RequiresConfirmation,
                escalationEnabled = action.EscalationEnabled,
                metadata = action.Metadata,
                createdAt = action.CreatedAt,
            })), ct);

        await voiceActionService.MarkDeliveredAsync(action.Id, ct);

        _logger.LogInformation(
            "Dispatched VoiceAction {Id} to user {UserId} via push+SSE",
            action.Id, action.UserId);
    }
}
