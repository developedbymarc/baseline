using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace backend.Entities;

public class Report
{
    public Report()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public ReportWindow Window { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    // json payload
    public string Payload { get; set; } = null!;
}
