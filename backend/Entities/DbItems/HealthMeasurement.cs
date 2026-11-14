using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class HealthMeasurement
{
    public HealthMeasurement()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string MetricCode { get; set; } = null!;
    public HealthMetric Metric { get; set; } = null!;

    public double Value { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public HealthMeasurementSource Source { get; set; }
    public double QualityScore { get; set; }
    public string Notes { get; set; } = null!;
    public DateTimeOffset SyncedAt { get; set; }
}
