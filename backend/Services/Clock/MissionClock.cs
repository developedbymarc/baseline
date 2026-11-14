using backend.Entities;

namespace backend.Services.Clock;

// WP 3.3. Mission time is derived from an anchor so it can run at any rate, pause and jump:
//   mission now = ClockAnchorMission + (real now - ClockAnchorReal) * ClockRate
// Every time-based feature (reminders, link delivery, age of data) reads this, never the wall clock.
public static class MissionClock
{
    // Spec G2 demo speed: 1 mission day per 5 real seconds.
    public const double DemoFastForwardRate = 86400.0 / 5.0;

    public static DateTimeOffset Now(Mission m, DateTimeOffset realNow)
    {
        if (m.ClockPaused) return m.ClockAnchorMission;
        var realElapsed = realNow - m.ClockAnchorReal;
        return m.ClockAnchorMission + TimeSpan.FromTicks((long)(realElapsed.Ticks * m.ClockRate));
    }

    // Freeze the current mission time into the anchor before changing rate / pause state.
    public static void Reanchor(Mission m, DateTimeOffset realNow)
    {
        m.ClockAnchorMission = Now(m, realNow);
        m.ClockAnchorReal = realNow;
    }

    public static void SetRate(Mission m, double rate, DateTimeOffset realNow)
    {
        if (rate <= 0 || double.IsNaN(rate) || double.IsInfinity(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), "Clock rate must be a positive number.");
        Reanchor(m, realNow);
        m.ClockRate = rate;
    }

    public static void SetPaused(Mission m, bool paused, DateTimeOffset realNow)
    {
        Reanchor(m, realNow);
        m.ClockPaused = paused;
    }

    public static void Advance(Mission m, TimeSpan by, DateTimeOffset realNow)
    {
        if (by < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(by), "The mission clock cannot go backwards.");
        Reanchor(m, realNow);
        m.ClockAnchorMission += by;
    }

    // Mission elapsed time (MET) since launch; negative before launch.
    public static TimeSpan Met(Mission m, DateTimeOffset missionNow) => missionNow - m.LaunchDate;
}

public sealed class MissionClockService(TimeProvider time)
{
    public DateTimeOffset RealNow => time.GetUtcNow();
    public DateTimeOffset Now(Mission m) => MissionClock.Now(m, time.GetUtcNow());
}
