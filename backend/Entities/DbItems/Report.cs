namespace backend.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    // Null = whole-crew report (ER notes).
    public Guid? CrewMemberId { get; set; }
    public CrewMember? CrewMember { get; set; }

    public ReportScope Scope { get; set; }
    public ReportWindow Window { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public string PayloadJson { get; set; } = "{}";
}
