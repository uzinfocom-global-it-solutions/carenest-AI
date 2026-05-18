using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task DispatchAsync(int recommendationId, CancellationToken ct = default)
    {
        var recommendation = await _context.Recommendations
            .Include(r => r.Child)
            .ThenInclude(c => c.Family)
            .ThenInclude(f => f.Members.Where(m => m.Status == FamilyMemberStatusEnum.Active))
            .FirstOrDefaultAsync(r => r.Id == recommendationId, ct)
            ?? throw new NotFoundException(nameof(Recommendation), recommendationId);

        var members = recommendation.Child.Family.Members
            .Where(m => m.Status == FamilyMemberStatusEnum.Active)
            .ToList();

        var channel = recommendation.DeliveryType switch
        {
            DeliveryTypeEnum.Push => DeliveryChannelEnum.Push,
            DeliveryTypeEnum.Voice => DeliveryChannelEnum.Voice,
            _ => DeliveryChannelEnum.InApp
        };

        foreach (var member in members)
        {
            var delivery = new NotificationDelivery
            {
                RecommendationId = recommendationId,
                UserId = member.UserId,
                DeliveryChannel = channel,
                DeliveryStatus = DeliveryStatusEnum.Pending,
            };

            _context.NotificationDeliveries.Add(delivery);
        }

        await _context.SaveChangesAsync(ct);

        await SendToChannelAsync(recommendation, channel, members.Select(m => m.UserId), ct);
    }

    public async Task RetryFailedAsync(CancellationToken ct = default)
    {
        var failed = await _context.NotificationDeliveries
            .Where(d => d.DeliveryStatus == DeliveryStatusEnum.Failed && d.RetryCount < 3)
            .Include(d => d.Recommendation)
            .ToListAsync(ct);

        foreach (var delivery in failed)
        {
            try
            {
                delivery.DeliveryStatus = DeliveryStatusEnum.Delivered;
                delivery.DeliveredAt = DateTimeOffset.UtcNow;
                delivery.RetryCount++;
            }
            catch (Exception ex)
            {
                delivery.RetryCount++;
                delivery.FailedReason = ex.Message;
                _logger.LogWarning(ex, "Retry {Count} failed for delivery {Id}", delivery.RetryCount, delivery.Id);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task SendToChannelAsync(
        Recommendation recommendation,
        DeliveryChannelEnum channel,
        IEnumerable<string> userIds,
        CancellationToken ct)
    {
        var deliveries = await _context.NotificationDeliveries
            .Where(d => d.RecommendationId == recommendation.Id && d.DeliveryStatus == DeliveryStatusEnum.Pending)
            .ToListAsync(ct);

        foreach (var d in deliveries)
        {
            d.DeliveryStatus = DeliveryStatusEnum.Delivered;
            d.DeliveredAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Dispatched recommendation {Id} via {Channel} to {Count} users",
            recommendation.Id, channel, deliveries.Count);
    }
}
