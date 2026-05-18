using Backend.Domain.Common;

namespace Backend.Domain.Events;

public class MedicationReminderEvent : BaseEvent
{
    public int MedicationScheduleId { get; }
    public int ChildId { get; }
    public string MedicationName { get; }
    public string Dosage { get; }
    public int FamilyId { get; }

    public MedicationReminderEvent(int medicationScheduleId, int childId, string medicationName, string dosage, int familyId)
    {
        MedicationScheduleId = medicationScheduleId;
        ChildId = childId;
        MedicationName = medicationName;
        Dosage = dosage;
        FamilyId = familyId;
    }
}
