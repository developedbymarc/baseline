using Microsoft.EntityFrameworkCore;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Missions;
using backend.Services.Reminders;

namespace backend.Services.Seed;

// Minimal deterministic seed so the API works end to end (replaces CSV import, spec section 16).
// ALL DATA IS SYNTHETIC. Member D's generator (WP 5.1-5.3) replaces the value model and plants the anomalies.
//
// Values are drawn around the middle of each metric's normal band from config/metrics.yaml, so no health
// numbers are hard-coded here.
public sealed class DemoSeeder(ApplicationDbContext db, HealthConfig config, MissionClockService clock, MissionService missions)
{
    public const int PreflightDays = 14;

    public async Task ResetAndSeedAsync(int seed = 42, CancellationToken ct = default)
    {
        await db.Database.EnsureDeletedAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        db.ChangeTracker.Clear();
        await SeedAsync(seed, ct);
    }

    public async Task SeedAsync(int seed = 42, CancellationToken ct = default)
    {
        var rng = new Random(seed);
        var realNow = clock.RealNow;
        var launch = new DateTimeOffset(realNow.UtcDateTime.Date, TimeSpan.Zero).AddHours(6);

        var mission = new Mission
        {
            Name = "Demo Mars transit (SYNTHETIC DATA)",
            Environment = MissionEnvironment.Transit,
            LaunchDate = launch,
            // Demo step 5: 12 min one-way.
            OneWayLatencySeconds = 12 * 60,
            LinkUp = true,
            Mode = MissionMode.Preflight,
            ClockAnchorReal = realNow,
            ClockAnchorMission = launch.AddDays(-1),
            ClockRate = 1.0,
        };
        db.Missions.Add(mission);

        var crew = new[]
        {
            new CrewMember { Name = "Alex Rivera (synthetic)", Role = "Commander", Age = 45, Sex = "M", HeightCm = 178 },
            new CrewMember { Name = "Sam Okafor (synthetic)", Role = "Flight engineer", Age = 38, Sex = "F", HeightCm = 165 },
            new CrewMember { Name = "Jordan Lee (synthetic)", Role = "Mission specialist", Age = 41, Sex = "M", HeightCm = 172 },
            new CrewMember { Name = "Rana Haddad (synthetic)", Role = "Medical officer", Age = 36, Sex = "F", HeightCm = 168 },
        };
        foreach (var c in crew) { c.MissionId = mission.Id; db.CrewMembers.Add(c); }

        foreach (var c in crew)
        {
            foreach (var metric in config.Metrics.Values)
            {
                var (center, spread) = PersonalModel(metric, rng);
                for (var day = PreflightDays; day >= 1; day--)
                {
                    var at = launch.AddDays(-day).AddHours(2 + rng.NextDouble());
                    db.Measurements.Add(new Measurement
                    {
                        CrewMemberId = c.Id,
                        MetricCode = metric.Code,
                        Value = Draw(metric, center, spread, rng),
                        RecordedAt = at,
                        Source = HealthMeasurementSource.Preflight,
                        SyncedAt = at,
                    });
                }

                if (metric.HasStatus)
                    db.ReminderRules.Add(new ReminderRule
                    {
                        CrewMemberId = c.Id,
                        MetricCode = metric.Code,
                        Schedule = new ReminderSchedule("daily", At: metric.Code == "sleep_h" ? "07:00" : "08:00").ToJson(),
                        CreatedBy = ReminderCreatedBy.Seed,
                    });
            }
        }

        await db.SaveChangesAsync(ct);

        // Lock baselines and switch to in-flight, then put the clock just after launch.
        await missions.StartMissionAsync(ct);
        var tracked = await db.Missions.SingleAsync(m => m.Id == mission.Id, ct);
        tracked.ClockAnchorReal = clock.RealNow;
        tracked.ClockAnchorMission = launch.AddMinutes(5);
        await db.SaveChangesAsync(ct);
    }

    // Personal mean near the middle of the normal band, day-to-day spread one eighth of that band.
    // Context inputs (polarity none) have no band: draw from the upper part of the plausible range so a
    // healthy seeded crew does not trip context rules (mood: 5 = best, 1 = worst; exercise well above a "missed" day).
    private static (double Center, double Spread) PersonalModel(HealthMetric m, Random rng)
    {
        if (m.Polarity == "none")
        {
            var range = m.PlausibleMax - m.PlausibleMin;
            return (m.PlausibleMin + range * (0.6 + 0.2 * rng.NextDouble()), range / 10);
        }
        var low = m.NormalLow ?? m.PlausibleMin;
        var high = m.NormalHigh ?? m.PlausibleMax;
        var width = high - low;
        var center = low + width * (0.4 + 0.2 * rng.NextDouble());
        return (center, width / 8);
    }

    private static double Draw(HealthMetric m, double center, double spread, Random rng)
    {
        // Box-Muller normal draw.
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        var v = Math.Clamp(center + z * spread, m.PlausibleMin, m.PlausibleMax);
        return IsWholeNumberMetric(m) ? Math.Round(v) : Math.Round(v, 1);
    }

    // Integer readings unless the metric's own limits use decimals; hours of sleep are always decimal.
    private static bool IsWholeNumberMetric(HealthMetric m) =>
        m.Unit != "h" && new[] { m.NormalLow, m.NormalHigh, m.WarnLow, m.WarnHigh, m.PlausibleMin, m.PlausibleMax }
            .All(x => x is null || x == Math.Floor(x.Value));
}
