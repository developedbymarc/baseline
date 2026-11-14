using backend.Entities;
using backend.Services.Clock;

namespace backend.Tests;

public class MissionClockTests
{
    private static readonly DateTimeOffset Real0 = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Launch = new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static Mission NewMission() => new()
    {
        Name = "t",
        LaunchDate = Launch,
        ClockAnchorReal = Real0,
        ClockAnchorMission = Launch,
        ClockRate = 1,
    };

    [Fact]
    public void Runs_at_real_time_by_default()
    {
        var m = NewMission();
        Assert.Equal(Launch.AddSeconds(30), MissionClock.Now(m, Real0.AddSeconds(30)));
    }

    [Fact]
    public void Demo_fast_forward_is_one_day_per_five_seconds()
    {
        var m = NewMission();
        MissionClock.SetRate(m, MissionClock.DemoFastForwardRate, Real0);
        Assert.Equal(Launch.AddDays(1), MissionClock.Now(m, Real0.AddSeconds(5)));
        Assert.Equal(Launch.AddDays(21), MissionClock.Now(m, Real0.AddSeconds(105)));
    }

    [Fact]
    public void Changing_rate_keeps_the_time_already_elapsed()
    {
        var m = NewMission();
        MissionClock.SetRate(m, 60, Real0.AddSeconds(10));        // 10 s at 1x
        Assert.Equal(Launch.AddSeconds(10 + 600), MissionClock.Now(m, Real0.AddSeconds(20))); // + 10 s at 60x
    }

    [Fact]
    public void Pause_freezes_and_resume_continues()
    {
        var m = NewMission();
        MissionClock.SetPaused(m, true, Real0.AddSeconds(10));
        Assert.Equal(Launch.AddSeconds(10), MissionClock.Now(m, Real0.AddHours(5)));
        MissionClock.SetPaused(m, false, Real0.AddHours(5));
        Assert.Equal(Launch.AddSeconds(15), MissionClock.Now(m, Real0.AddHours(5).AddSeconds(5)));
    }

    [Fact]
    public void Advance_jumps_forward_and_never_back()
    {
        var m = NewMission();
        MissionClock.Advance(m, TimeSpan.FromDays(3), Real0);
        Assert.Equal(Launch.AddDays(3), MissionClock.Now(m, Real0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MissionClock.Advance(m, TimeSpan.FromSeconds(-1), Real0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Invalid_rates_are_rejected(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MissionClock.SetRate(NewMission(), rate, Real0));
    }

    [Fact]
    public void Met_is_time_since_launch()
    {
        var m = NewMission();
        Assert.Equal(TimeSpan.FromHours(-1), MissionClock.Met(m, Launch.AddHours(-1)));
        Assert.Equal(TimeSpan.FromDays(2), MissionClock.Met(m, Launch.AddDays(2)));
    }
}
