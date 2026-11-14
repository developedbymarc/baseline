using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Entities;

namespace backend.Services.Reminders;

// REMINDER_RULE.schedule as JSON (story B1). Times of day are UTC; day counting starts at the launch date.
//   {"kind":"daily","at":"08:00"}
//   {"kind":"every_n_days","n":3,"at":"08:00"}
//   {"kind":"weekly","at":"08:00"}            (every 7 days from launch)
//   {"kind":"one_off","on":"2026-11-20T08:00:00Z"}
public sealed record ReminderSchedule(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("at")] string? At = null,
    [property: JsonPropertyName("n")] int? N = null,
    [property: JsonPropertyName("on")] DateTimeOffset? On = null)
{
    public static ReminderSchedule Parse(string json)
    {
        var s = JsonSerializer.Deserialize<ReminderSchedule>(json)
                ?? throw new FormatException("Empty reminder schedule.");
        s.Validate();
        return s;
    }

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    private int IntervalDays => Kind switch
    {
        "daily" => 1,
        "weekly" => 7,
        "every_n_days" => N ?? 0,
        _ => 0,
    };

    private TimeSpan TimeOfDay => TimeSpan.ParseExact(At!, @"hh\:mm", CultureInfo.InvariantCulture);

    public void Validate()
    {
        switch (Kind)
        {
            case "daily" or "weekly" or "every_n_days":
                if (At is null || !TimeSpan.TryParseExact(At, @"hh\:mm", CultureInfo.InvariantCulture, out _))
                    throw new FormatException("'at' must be HH:mm.");
                if (Kind == "every_n_days" && N is not > 0)
                    throw new FormatException("'n' must be a positive number of days.");
                break;
            case "one_off":
                if (On is null) throw new FormatException("'on' is required for one_off.");
                break;
            default:
                throw new FormatException($"Unknown schedule kind '{Kind}'.");
        }
    }

    // Due times in [from, to], starting no earlier than the launch day.
    public IEnumerable<DateTimeOffset> Occurrences(DateTimeOffset launchDate, DateTimeOffset from, DateTimeOffset to)
    {
        if (Kind == "one_off")
        {
            if (On >= from && On <= to) yield return On.Value;
            yield break;
        }

        var day0 = new DateTimeOffset(launchDate.UtcDateTime.Date, TimeSpan.Zero) + TimeOfDay;
        var step = TimeSpan.FromDays(IntervalDays);
        var k = from <= day0 ? 0 : (long)Math.Ceiling((from - day0).Ticks / (double)step.Ticks);
        for (var t = day0 + step * k; t <= to; t += step)
            if (t >= from) yield return t;
    }
}

// Pure reminder rules (B2, B3). Reminders live on the crew side and never go through the link.
public static class ReminderRules
{
    // How early a measurement may count for a reminder, and how long after due it still counts as on time.
    public static readonly TimeSpan EarlyWindow = TimeSpan.FromHours(2);
    public static readonly TimeSpan OnTimeGrace = TimeSpan.FromHours(1);
    public static readonly TimeSpan OneOffMissedAfter = TimeSpan.FromDays(1);

    // Completing a measurement clears the oldest open reminder for that metric (B2).
    public static ReminderInstance? Complete(IEnumerable<ReminderInstance> openForMetric, DateTimeOffset recordedAt)
    {
        var target = openForMetric
            .Where(i => i.Status == ReminderStatus.Pending && i.DueAt <= recordedAt + EarlyWindow)
            .OrderBy(i => i.DueAt)
            .FirstOrDefault();
        if (target is null) return null;
        target.Status = recordedAt <= target.DueAt + OnTimeGrace ? ReminderStatus.Done : ReminderStatus.Late;
        target.CompletedAt = recordedAt;
        return target;
    }

    // A pending instance is missed once the next instance of the same rule is due (or a day after a one-off).
    public static void MarkMissed(IReadOnlyList<ReminderInstance> ruleInstancesByDue, DateTimeOffset missionNow, bool oneOff)
    {
        for (var i = 0; i < ruleInstancesByDue.Count; i++)
        {
            var inst = ruleInstancesByDue[i];
            if (inst.Status != ReminderStatus.Pending) continue;
            var cutoff = oneOff || i + 1 >= ruleInstancesByDue.Count
                ? (oneOff ? inst.DueAt + OneOffMissedAfter : (DateTimeOffset?)null)
                : ruleInstancesByDue[i + 1].DueAt;
            if (cutoff is { } c && c <= missionNow) inst.Status = ReminderStatus.Missed;
        }
    }

    public static bool IsOverdue(ReminderInstance i, DateTimeOffset missionNow) =>
        i.Status == ReminderStatus.Pending && i.DueAt < missionNow;
}
