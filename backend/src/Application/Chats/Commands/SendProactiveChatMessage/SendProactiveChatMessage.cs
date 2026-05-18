using System.Text;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Chats.Commands.SendProactiveChatMessage;

public record SendProactiveChatMessageCommand(
    int FamilyId,
    string Locale = "en") : IRequest<bool>;

public class SendProactiveChatMessageCommandHandler
    : IRequestHandler<SendProactiveChatMessageCommand, bool>
{
    private const int CooldownHours = 3;

    private readonly IApplicationDbContext _context;

    public SendProactiveChatMessageCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<bool> Handle(
        SendProactiveChatMessageCommand request,
        CancellationToken ct)
    {
        var chat = await _context.Chats
            .Where(c => c.FamilyId == request.FamilyId)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (chat is null) return false;

        var since = DateTimeOffset.UtcNow.AddHours(-CooldownHours);
        var recentProactive = await _context.ChatMessages
            .AnyAsync(m => m.ChatId == chat.Id
                        && m.SenderType == SenderTypeEnum.Ai
                        && m.MessageType == MessageTypeEnum.Message
                        && m.Content.StartsWith("[proactive]")
                        && m.CreatedAt >= since, ct);
        if (recentProactive) return false;

        var children = await _context.Children
            .Include(c => c.Sensitivity)
            .Where(c => c.FamilyId == request.FamilyId)
            .ToListAsync(ct);
        if (children.Count == 0) return false;

        var weather = await _context.WeatherSnapshots
            .OrderByDescending(w => w.CollectedAt)
            .FirstOrDefaultAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var upcomingEvents = await _context.CalendarEvents
            .Where(e => e.FamilyId == request.FamilyId
                     && e.StartDatetime >= now
                     && e.StartDatetime <= now.AddHours(4))
            .ToListAsync(ct);

        var isRu = request.Locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var alerts = BuildAlerts(children, weather, upcomingEvents, isRu);
        if (alerts.Count == 0) return false;

        var text = "[proactive] " + (isRu
            ? $"Привет! Вот что важно прямо сейчас:\n\n{string.Join("\n", alerts)}"
            : $"Hey! Here's what you should know right now:\n\n{string.Join("\n", alerts)}");

        var msg = new ChatMessage
        {
            ChatId = chat.Id,
            UserId = null,
            SenderType = SenderTypeEnum.Ai,
            MessageType = MessageTypeEnum.Message,
            InputMode = InputModeEnum.Text,
            Content = text,
            AiResponse = text,
            Successful = true,
        };

        _context.ChatMessages.Add(msg);
        chat.UpdatedAt = now;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static List<string> BuildAlerts(
        List<Child> children,
        WeatherSnapshot? w,
        List<CalendarEvent> events,
        bool isRu)
    {
        var alerts = new List<string>();

        foreach (var child in children)
        {
            var s = child.Sensitivity;
            var name = child.DisplayName;

            if (w != null)
            {
                if (w.UvIndex >= 6 && s?.UvSensitive == true)
                    alerts.Add(isRu
                        ? $"☀️ УФ-индекс {w.UvIndex:F0} — у {name} чувствительность к UV. Нанесите солнцезащитный крем перед выходом."
                        : $"☀️ UV index is {w.UvIndex:F0} — {name} is UV-sensitive. Apply sunscreen before going out.");

                if (w.TemperatureC <= 5 && s?.ColdSensitive == true)
                    alerts.Add(isRu
                        ? $"🧥 На улице {w.TemperatureC:F0}°C — {name} чувствителен к холоду. Оденьте тёплые слои."
                        : $"🧥 It's {w.TemperatureC:F0}°C outside — {name} is cold-sensitive. Dress in warm layers.");

                if (w.TemperatureC >= 30 && s?.HeatSensitive == true)
                    alerts.Add(isRu
                        ? $"🌡️ Жара {w.TemperatureC:F0}°C — {name} чувствителен к теплу. Давайте больше воды и держите в тени."
                        : $"🌡️ It's {w.TemperatureC:F0}°C — {name} is heat-sensitive. Keep hydrated and in the shade.");

                if (!string.IsNullOrEmpty(w.Aqi)
                    && int.TryParse(w.Aqi, out var aqi) && aqi > 100
                    && s?.AirQualitySensitive == true)
                    alerts.Add(isRu
                        ? $"😷 AQI = {aqi} (плохое качество воздуха) — у {name} чувствительность дыхательных путей. Сведите прогулки к минимуму."
                        : $"😷 AQI is {aqi} (poor air) — {name} has respiratory sensitivity. Limit outdoor time.");

                if (!string.IsNullOrEmpty(w.PollenLevel)
                    && w.PollenLevel.Equals("high", StringComparison.OrdinalIgnoreCase)
                    && s?.PollenSensitive == true)
                    alerts.Add(isRu
                        ? $"🌿 Уровень пыльцы высокий — у {name} аллергия. Следите за симптомами."
                        : $"🌿 Pollen level is high — {name} has pollen allergy. Watch for symptoms.");

                if (w.WeatherCondition == WeatherConditionEnum.Rainy)
                    alerts.Add(isRu
                        ? $"🌧️ Ожидается дождь. Возьмите дождевик для {name}."
                        : $"🌧️ Rain expected. Pack a raincoat for {name}.");

                if (w.WindKmh >= 40 || w.WeatherCondition == WeatherConditionEnum.Storm)
                    alerts.Add(isRu
                        ? $"⚠️ Сильный ветер / гроза — лучше остаться дома с {name}."
                        : $"⚠️ Strong winds / storm expected — best to stay indoors with {name}.");
            }
        }

        foreach (var ev in events)
        {
            var child = children.FirstOrDefault(c => c.Id == ev.ChildId);
            var childName = child?.DisplayName ?? "your child";
            var mins = (int)(ev.StartDatetime - DateTimeOffset.UtcNow).TotalMinutes;
            var when = mins < 60 ? (isRu ? $"через {mins} мин" : $"in {mins} min") : (isRu ? $"через {mins / 60}ч {mins % 60}м" : $"in {mins / 60}h {mins % 60}m");

            if (ev.LocationType == LocationTypeEnum.Outdoor && w?.WeatherCondition == WeatherConditionEnum.Rainy)
                alerts.Add(isRu
                    ? $"📅 Скоро ({when}) — «{ev.Title}» для {childName} на улице, а дождь идёт. Возьмите дождевик!"
                    : $"📅 Coming up ({when}) — '{ev.Title}' for {childName} is outdoor but it's raining. Bring a raincoat!");
            else if (ev.WeatherSensitive)
                alerts.Add(isRu
                    ? $"📅 «{ev.Title}» ({childName}) начинается {when}. Событие зависит от погоды — проверьте условия."
                    : $"📅 '{ev.Title}' for {childName} starts {when}. It's weather-sensitive — check conditions.");
            else
                alerts.Add(isRu
                    ? $"📅 «{ev.Title}» ({childName}) начинается {when}."
                    : $"📅 '{ev.Title}' for {childName} starts {when}.");
        }

        return alerts;
    }
}
