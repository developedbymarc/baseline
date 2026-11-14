namespace backend.Entities;

public class ProtocolRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrewMemberId { get; set; }
    public CrewMember CrewMember { get; set; } = null!;

    // Id from a protocol YAML file (e.g. "chest_pain").
    public string ProtocolId { get; set; } = null!;
    // Version of the protocol YAML when the run started (audit trail; beyond ER Fig. 5C).
    public string ProtocolVersion { get; set; } = null!;

    public Guid? AlertId { get; set; }
    public Alert? Alert { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    // JSON array of { nodeId, answer, at } (F3: steps with timestamps).
    public string StepsJson { get; set; } = "[]";
}
