using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class EnvironmentLog
{
    public EnvironmentLog()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }

    public EnvironmentMetric Metric { get; set; }
    public double Value { get; set; }

    [Required]
    public DateTimeOffset RecordedAt { get; set; }
}
