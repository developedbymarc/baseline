using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class Baseline
{
    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string MetricCode { get; set; } = null!;
    public HealthMetric Metric { get; set; } = null!;

    public double Mean { get; set; }
    public double StandardDeviation { get; set; }
    public double SampleSize { get; set; }

    public string Method => SampleSize switch
    {
        > 10 => "sample",
        _ => "shrunk"
    };

    public DateTimeOffset LockedAt { get; set; }
    public bool IsLocked => DateTimeOffset.UtcNow >= LockedAt;
}
