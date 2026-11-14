namespace backend.Entities;

public class SymptomLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    public string SymptomCode { get; set; } = null!;
    public DateTimeOffset LoggedAt { get; set; }
    public string? Note { get; set; }
}
