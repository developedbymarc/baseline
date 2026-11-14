using backend.Entities;
using backend.Services.Reminders;

namespace backend.Tests;

public class ReminderTests
{
    private static readonly DateTimeOffset Launch = new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day0 = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Daily_occurrences_from_launch()
    {
        var s = ReminderSchedule.Parse("""{"kind":"daily","at":"08:00"}""");
        var times = s.Occurrences(Launch, Launch, Launch.AddDays(2)).ToList();
        Assert.Equal([Day0.AddHours(8), Day0.AddDays(1).AddHours(8)], times);
    }

    [Fact]
    public void Every_n_days_and_weekly()
    {
        var every3 = ReminderSchedule.Parse("""{"kind":"every_n_days","n":3,"at":"09:30"}""");
        Assert.Equal(
            [Day0.AddHours(9.5), Day0.AddDays(3).AddHours(9.5), Day0.AddDays(6).AddHours(9.5)],
            every3.Occurrences(Launch, Launch, Day0.AddDays(7)).ToList());

        var weekly = ReminderSchedule.Parse("""{"kind":"weekly","at":"08:00"}""");
        Assert.Equal(3, weekly.Occurrences(Launch, Launch, Day0.AddDays(15)).Count());
    }

    [Fact]
    public void Occurrences_window_start_is_inclusive_and_skips_earlier()
    {
        var s = ReminderSchedule.Parse("""{"kind":"daily","at":"08:00"}""");
        var from = Day0.AddDays(5).AddHours(8);
        Assert.Equal(from, s.Occurrences(Launch, from, from.AddHours(1)).Single());
    }

    [Fact]
    public void One_off()
    {
        var s = ReminderSchedule.Parse("""{"kind":"one_off","on":"2026-11-20T08:00:00Z"}""");
        Assert.Single(s.Occurrences(Launch, Launch, Launch.AddDays(10)));
        Assert.Empty(s.Occurrences(Launch, Launch, Launch.AddDays(2)));
    }

    [Theory]
    [InlineData("""{"kind":"hourly","at":"08:00"}""")]
    [InlineData("""{"kind":"daily"}""")]
    [InlineData("""{"kind":"daily","at":"8am"}""")]
    [InlineData("""{"kind":"every_n_days","at":"08:00"}""")]
    [InlineData("""{"kind":"every_n_days","n":0,"at":"08:00"}""")]
    [InlineData("""{"kind":"one_off"}""")]
    public void Invalid_schedules_are_rejected(string json)
    {
        Assert.Throws<FormatException>(() => ReminderSchedule.Parse(json));
    }

    [Fact]
    public void Schedule_round_trips_through_json()
    {
        var s = new ReminderSchedule("every_n_days", At: "07:15", N: 2);
        Assert.Equal(s, ReminderSchedule.Parse(s.ToJson()));
    }

    private static ReminderInstance Due(DateTimeOffset at) => new() { DueAt = at };

    [Fact]
    public void Measurement_on_time_completes_oldest_open_reminder()
    {
        var a = Due(Day0.AddHours(8));
        var b = Due(Day0.AddDays(1).AddHours(8));
        var done = ReminderRules.Complete([b, a], Day0.AddHours(8).AddMinutes(30));
        Assert.Same(a, done);
        Assert.Equal(ReminderStatus.Done, a.Status);
        Assert.Equal(ReminderStatus.Pending, b.Status);
    }

    [Fact]
    public void Measurement_after_grace_is_late()
    {
        var a = Due(Day0.AddHours(8));
        ReminderRules.Complete([a], Day0.AddHours(10));
        Assert.Equal(ReminderStatus.Late, a.Status);
    }

    [Fact]
    public void Measurement_too_early_does_not_count()
    {
        var a = Due(Day0.AddHours(8));
        Assert.Null(ReminderRules.Complete([a], Day0.AddHours(5)));
        Assert.Equal(ReminderStatus.Pending, a.Status);
    }

    [Fact]
    public void Pending_reminder_is_missed_once_the_next_one_is_due()
    {
        var a = Due(Day0.AddHours(8));
        var b = Due(Day0.AddDays(1).AddHours(8));
        ReminderRules.MarkMissed([a, b], Day0.AddDays(1).AddHours(7), oneOff: false);
        Assert.Equal(ReminderStatus.Pending, a.Status);
        ReminderRules.MarkMissed([a, b], Day0.AddDays(1).AddHours(8), oneOff: false);
        Assert.Equal(ReminderStatus.Missed, a.Status);
        Assert.Equal(ReminderStatus.Pending, b.Status);
    }

    [Fact]
    public void Overdue_flag()
    {
        var a = Due(Day0.AddHours(8));
        Assert.False(ReminderRules.IsOverdue(a, Day0.AddHours(8)));
        Assert.True(ReminderRules.IsOverdue(a, Day0.AddHours(8).AddMinutes(1)));
    }
}
