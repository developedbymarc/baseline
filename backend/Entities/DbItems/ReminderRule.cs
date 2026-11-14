using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class ReminderRule
{
    public ReminderRule()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string MetricCode { get; set; } = null!;
    public HealthMetric Metric { get; set; } = null!;

    public int IntervalInDays { get; set; }
}
