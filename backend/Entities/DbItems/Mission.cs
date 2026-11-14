namespace backend.Entities;

public class Mission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;
    public MissionEnvironment Environment { get; set; }
    public DateTimeOffset LaunchDate { get; set; }

    // Latency simulator (spec 2.1, E2, E3, G3).
    public double OneWayLatencySeconds { get; set; }
    public bool LinkUp { get; set; } = true;

    public MissionMode Mode { get; set; } = MissionMode.Preflight;

    // Mission clock (WP 3.3). Replaces the single mission_clock_offset column of the ER diagram:
    // mission now = ClockAnchorMission + (real now - ClockAnchorReal) * ClockRate, unless paused.
    public DateTimeOffset ClockAnchorReal { get; set; }
    public DateTimeOffset ClockAnchorMission { get; set; }
    public double ClockRate { get; set; } = 1.0;
    public bool ClockPaused { get; set; }

    public List<CrewMember> Crew { get; set; } = [];
}
