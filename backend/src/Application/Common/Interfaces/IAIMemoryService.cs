using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IAIMemoryService
{
    Task RecordObservationAsync(int familyId, string key, string value, AIMemoryType type, CancellationToken ct);
    Task<List<AIMemory>> GetFamilyMemoryAsync(int familyId, CancellationToken ct);
    Task<string> BuildMemoryContextAsync(int familyId, CancellationToken ct);
    Task RecordIgnoredReminderAsync(int familyId, string reminderType, CancellationToken ct);
    Task RecordConfirmedReminderAsync(int familyId, string reminderType, CancellationToken ct);
}
