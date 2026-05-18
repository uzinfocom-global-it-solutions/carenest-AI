using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IPushNotificationService
{
    Task<bool> SendAsync(string deviceToken, string title, string body, NotificationPriority priority, Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task<int> SendToUserAsync(string userId, string title, string body, NotificationPriority priority, Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task<int> SendToFamilyAsync(int familyId, string title, string body, NotificationPriority priority, Dictionary<string, string>? data = null, CancellationToken ct = default);
    Task<bool> SendToTopicAsync(string topic, string title, string body, NotificationPriority priority, Dictionary<string, string>? data = null, CancellationToken ct = default);
}
