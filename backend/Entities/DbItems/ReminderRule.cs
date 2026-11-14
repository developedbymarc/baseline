namespace backend.Entities;

public class ReminderRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string MetricCode { get; set; } = null!;

    // JSON, see Services/Reminders/ReminderSchedule.cs, e.g. {"kind":"daily","at":"08:00"}.
    public string Schedule { get; set; } = null!;
    public ReminderCreatedBy CreatedBy { get; set; }
}
