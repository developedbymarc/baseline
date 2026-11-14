namespace backend.Entities;

public class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    // Code from config/metrics.yaml (validated in code, not a DB foreign key).
    public string MetricCode { get; set; } = null!;

    public double Value { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public HealthMeasurementSource Source { get; set; }
    public QualityFlag QualityFlag { get; set; } = QualityFlag.Ok;
    public string? Note { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
}
