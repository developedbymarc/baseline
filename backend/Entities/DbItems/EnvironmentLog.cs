namespace backend.Entities;

public class EnvironmentLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    // Free string (e.g. "co2_mmhg") so the context rules file can add kinds without code changes.
    public string Kind { get; set; } = null!;
    public double Value { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
