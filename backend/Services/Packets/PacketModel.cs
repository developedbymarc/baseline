using backend.Entities;
using backend.Services.Engines;

namespace backend.Services.Packets;

// The SBAR handoff packet (work package 4.5, stories H1-H3) as one JSON object. Ground reads everything
// from this object alone; `text` is the same content rendered by Templates/sbar_packet.sbn.
public sealed record HandoffPacket(
    string Schema,
    DateTimeOffset CreatedAt,
    string CreatedMet,
    string CreatedUtc,
    Guid CrewMemberId,
    string CrewName,
    Guid? AlertId,
    Guid? RunId,
    string? Note,
    PacketData Data,
    string Text);

public sealed record PacketData(
    PacketMission Mission,
    PacketCrew Crew,
    PacketTrigger Trigger,
    IReadOnlyList<PacketMeasurement> Measurements72h,
    IReadOnlyList<PacketBaseline> Baselines,
    PacketProtocol? Protocol,
    IReadOnlyList<ContextFactor> ContextFactors,
    PacketContextOutcome? ContextOutcome,
    IReadOnlyList<PacketFollowUp> FollowUp,
    string Recommendation);

public sealed record PacketMission(
    string Name, MissionEnvironment Environment, double OneWayLatencySeconds, string OneWayLatencyText,
    bool LinkUp, int MissionDay, bool InAdaptationWindow);

public sealed record PacketCrew(Guid Id, string Name, string Role, int Age, string Sex);

// kind: red_reading | symptom | protocol
public sealed record PacketTrigger(
    string Kind, string Summary, PacketAlert? Alert, IReadOnlyList<PacketSymptom> Symptoms);

public sealed record PacketAlert(
    Guid Id, StatusLevel Level, string Reason, string RuleId, DateTimeOffset At, string AtMet,
    string? MetricCode, string? MetricName, double? Value, string? Unit);

public sealed record PacketSymptom(Guid Id, string SymptomCode, string? Note, DateTimeOffset LoggedAt, string LoggedMet);

public sealed record PacketMeasurement(
    string MetricCode, string MetricName, string Unit, double Value, DateTimeOffset RecordedAt, string RecordedMet,
    StatusLevel? Level, string? Rule);

public sealed record PacketBaseline(
    string MetricCode, string MetricName, string Unit, double Mean, double SdEffective, int N, bool Shrunk,
    double? LatestValue, DateTimeOffset? LatestAt, double? LatestZ, string Summary);

public sealed record PacketProtocol(
    Guid RunId, string ProtocolId, string Title, string Version, DateTimeOffset StartedAt, string StartedMet,
    DateTimeOffset? EndedAt, IReadOnlyList<PacketStep> Steps);

public sealed record PacketStep(string NodeId, string Text, string? Answer, DateTimeOffset At, string AtMet);

public sealed record PacketContextOutcome(StatusLevel OriginalLevel, StatusLevel Level, bool Downgraded, IReadOnlyList<string> DowngradedBy);

// Pre-answered follow-up question (H3). FilledFrom says where the answer came from, or "not recorded".
public sealed record PacketFollowUp(string Key, string Question, string Answer, string FilledFrom);
