namespace backend.Entities;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public Guid? MeasurementId { get; set; }
    public Measurement? Measurement { get; set; }

    public StatusLevel Level { get; set; }
    public string Reason { get; set; } = null!;
    // Which status rule fired: "threshold", "zscore" or "threshold+zscore".
    public string RuleId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string? AcknowledgedBy { get; set; }

    // Context engine result as JSON: factors with sources, and whether / why the flag was downgraded.
    // Stored here instead of ALERT_FACTOR rows (that table is undecided). Null for symptom alerts.
    public string? ContextJson { get; set; }
}
