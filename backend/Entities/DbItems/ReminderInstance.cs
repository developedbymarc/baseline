namespace backend.Entities;

public class ReminderInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RuleId { get; set; }
    public ReminderRule Rule { get; set; } = null!;

    public DateTimeOffset DueAt { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public DateTimeOffset? CompletedAt { get; set; }
}
