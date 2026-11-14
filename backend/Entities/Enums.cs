namespace backend.Entities;

// Spec A1: transit, station, lunar surface.
public enum MissionEnvironment
{
    Transit,
    Station,
    LunarSurface,
}

public enum MissionMode
{
    Preflight,
    Inflight,
}

public enum HealthMeasurementSource
{
    Preflight,
    Inflight,
}

public enum QualityFlag
{
    Ok,
    // C3: value was outside the plausible range and the crew member confirmed it anyway.
    ImplausibleConfirmed,
}

public enum ReportScope
{
    Member,
    Crew,
}

// Team decision (follows the ER diagrams): only biweekly and monthly reports exist.
public enum ReportWindow
{
    Biweekly,
    Monthly,
}

public enum ReminderStatus
{
    Pending,
    Done,
    Late,
    Missed,
}

public enum ReminderCreatedBy
{
    Ground,
    Seed,
}

public enum StatusLevel
{
    Green = 0,
    Yellow = 1,
    Red = 2,
}

public enum LinkDirection
{
    CrewToGround,
    GroundToCrew,
}

// Packet (H2) and alert (E2) are crew -> ground; reply (H4) is optional, ground -> crew.
public enum LinkMessageType
{
    Alert,
    Packet,
    Reply,
}
