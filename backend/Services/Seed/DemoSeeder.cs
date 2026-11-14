using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Engines;
using backend.Services.Link;
using backend.Services.Missions;
using backend.Services.Reminders;

namespace backend.Services.Seed;

// Demo mission seed (WP 3.5, story G1; replaces CSV import, spec section 16). ALL DATA IS SYNTHETIC.
//
// 4 crew, 14 pre-flight days and 90 in-flight days. Baselines are locked at mission start and the mission
// clock starts 5 minutes after launch, so the in-flight history lies ahead of the clock and "happens" as the
// presenter fast-forwards (endpoints hide anything later than mission time now).
//
// Vital signs are drawn around the middle of each metric's normal band from config/metrics.yaml, with a
// day-to-day spread of one eighth of that band; no health limits are hard-coded here. The few numbers below
// (drift rates, fever size, mood and exercise levels) describe the synthetic story, not health thresholds.
//
// Planted events (spec section 11 D):
//   crew 1  slow systolic / diastolic BP drift ramping over the mission: green first third, yellow middle, red end
//   crew 2  sleep decline from day 30 (low from day 36) with a mood dip on days 36-45; cabin CO2 entry on day 38
//   crew 3  one-day fever on day 52 with a heart-rate rise the same morning
//   crew 4  missed-measurement streak: nothing logged on days 60-64
//   all     geomagnetic storm (Kp 6) and a solar particle event on day 47
public sealed class DemoSeeder(ApplicationDbContext db, HealthConfig config, MissionClockService clock, MissionService missions)
{
    public const int PreflightDays = 14;
    public const int InflightDays = 90;
    public const string SyntheticLabel = "SYNTHETIC";

    // In flight, day-to-day noise is this share of the pre-flight spread (readings are taken the same way every
    // morning on board). With the full spread, about 13 % of normal readings would cross |z| > 1.5 and the demo
    // would drown in yellow flags; 0.7 keeps ordinary noise mostly green so the planted events stand out.
    public const double InflightNoiseFactor = 0.7;

    // Synthetic story parameters.
    // Alex's BP drift, in SDs of his own pre-flight baseline, as a continuous ramp through these knots
    // (mission day, SD above baseline): green for about the first third (z < 1.5), yellow in the middle,
    // red near the end (z > 2.5). Applied to systolic and diastolic BP alike.
    public static readonly (int Day, double Sd)[] BpDriftKnots = [(1, 0.0), (27, 1.0), (33, 1.9), (60, 2.05), (64, 3.0), (90, 3.5)];
    // Day-to-day noise of Alex's in-flight BP, as a share of his pre-flight SD. Lower than the general factor
    // so the green / yellow / red bands of the ramp read clearly: the yellow band is 1 SD wide, which for a
    // personal SD of 3-4 mmHg is only about three whole-mmHg readings.
    public const double BpDriftNoiseFactor = 0.25;
    public const int SleepDeclineStartDay = 30, SleepLowFromDay = 36, SleepLowUntilDay = 45;
    public const double SleepLowHours = 4.8;
    public const int MoodDipFromDay = 36, MoodDipUntilDay = 45;
    public const int Co2Day = 38;
    public const double Co2Mmhg = 3.6;
    public const int FeverDay = 52;
    public const double FeverTempC = 38.6, FeverHrRise = 14;
    public const int MissedFromDay = 60, MissedUntilDay = 64;
    // In the middle third, when Alex's BP is yellow most days, so the space-weather factors show on real flags.
    public const int SpaceWeatherDay = 47;
    public const double StormKp = 6;
    public const double MoodMean = 4.3, MoodSd = 0.4;          // 1 = worst, 5 = best
    public const double ExerciseMeanMin = 90, ExerciseSdMin = 12; // daily countermeasure exercise

    // Time of day (UTC) of each daily reading. Launch is 06:00, so these all fall inside the mission day.
    private static readonly Dictionary<string, TimeSpan> ReadingTime = new()
    {
        ["sleep_h"] = new(7, 10, 0), ["temp"] = new(7, 55, 0), ["hr_rest"] = new(8, 5, 0), ["bp_sys"] = new(8, 10, 0),
        ["bp_dia"] = new(8, 10, 30), ["spo2"] = new(8, 15, 0), ["exercise_min"] = new(19, 0, 0), ["mood"] = new(21, 0, 0),
    };

    public async Task ResetAndSeedAsync(int seed = 42, CancellationToken ct = default)
    {
        await db.Database.EnsureDeletedAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        db.ChangeTracker.Clear();
        await SeedAsync(seed, ct);
    }

    public async Task SeedAsync(int seed = 42, CancellationToken ct = default)
    {
        var realNow = clock.RealNow;
        var launch = new DateTimeOffset(realNow.UtcDateTime.Date, TimeSpan.Zero).AddHours(6);

        var mission = new Mission
        {
            Name = $"Demo Mars transit ({SyntheticLabel} DATA)",
            Environment = MissionEnvironment.Transit,
            LaunchDate = launch,
            OneWayLatencySeconds = 12 * 60, // demo step 5: 12 min one way
            LinkUp = true,
            Mode = MissionMode.Preflight,
            ClockAnchorReal = realNow,
            ClockAnchorMission = launch.AddMinutes(-1),
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
        var stories = new[]
        {
            "Planted: slow BP drift from mission day 8.",
            "Planted: sleep decline from day 30 with a mood dip on days 36-45.",
            "Planted: one-day fever on day 52 with a heart-rate rise.",
            "Planted: no measurements logged on days 60-64.",
        };
        for (var i = 0; i < crew.Length; i++)
        {
            crew[i].MissionId = mission.Id;
            crew[i].Notes = $"{SyntheticLabel} demo crew member. {stories[i]}";
            db.CrewMembers.Add(crew[i]);
        }

        var readings = new List<Measurement>();
        for (var c = 0; c < crew.Length; c++)
            foreach (var metric in config.Metrics.Values)
                readings.AddRange(Series(crew[c], c, metric, launch, seed));
        db.Measurements.AddRange(readings);

        foreach (var member in crew)
            foreach (var metric in config.Metrics.Values.Where(m => m.HasStatus))
                db.ReminderRules.Add(new ReminderRule
                {
                    CrewMemberId = member.Id,
                    MetricCode = metric.Code,
                    Schedule = new ReminderSchedule("daily", At: metric.Code == "sleep_h" ? "07:00" : "08:00").ToJson(),
                    CreatedBy = ReminderCreatedBy.Seed,
                });

        var environment = new List<EnvironmentLog>
        {
            new() { MissionId = mission.Id, Kind = "kp", Value = StormKp, RecordedAt = Day(launch, SpaceWeatherDay, new TimeSpan(6, 30, 0)) },
            new() { MissionId = mission.Id, Kind = "solar_particle_event", Value = 1, RecordedAt = Day(launch, SpaceWeatherDay, new TimeSpan(6, 45, 0)) },
            new() { MissionId = mission.Id, Kind = "co2_mmhg", Value = Co2Mmhg, RecordedAt = Day(launch, Co2Day, new TimeSpan(6, 20, 0)) },
        };
        db.EnvironmentLogs.AddRange(environment);
        await db.SaveChangesAsync(ct);

        // Lock the baselines at mission start (launch) and switch to in-flight.
        await missions.StartMissionAsync(ct);

        // Precompute the alerts the crew instance would have raised for the seeded in-flight readings, with
        // the real status and context engines, each sent to ground through the link with the mission latency.
        var tracked = await db.Missions.SingleAsync(m => m.Id == mission.Id, ct);
        AddSeededAlerts(tracked, crew, readings, environment);

        tracked.ClockAnchorReal = clock.RealNow;
        tracked.ClockAnchorMission = launch.AddMinutes(5);
        await db.SaveChangesAsync(ct);
    }

    private static DateTimeOffset Day(DateTimeOffset launch, int missionDay, TimeSpan timeOfDay) =>
        new DateTimeOffset(launch.UtcDateTime.Date, TimeSpan.Zero).AddDays(missionDay - 1) + timeOfDay;

    // One deterministic random stream per crew member and metric, so changing one series never shifts another.
    private static Random Stream(int seed, int crewIndex, string metric) =>
        new(unchecked(seed * 7919 + crewIndex * 104729 + metric.Aggregate(17, (h, ch) => h * 31 + ch)));

    private static double Normal(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private IEnumerable<Measurement> Series(CrewMember crew, int crewIndex, HealthMetric metric, DateTimeOffset launch, int seed)
    {
        var rng = Stream(seed, crewIndex, metric.Code);
        var (center, spread) = PersonalModel(metric, rng);
        var latency = TimeSpan.FromSeconds(12 * 60);

        // Pre-flight: the 14 calendar days before launch (entered on the ground, so already synced).
        var preflight = new List<Measurement>();
        for (var k = PreflightDays; k >= 1; k--)
        {
            var at = new DateTimeOffset(launch.UtcDateTime.Date, TimeSpan.Zero).AddDays(-k) + ReadingTime[metric.Code];
            var value = Clamp(metric, center + Normal(rng) * spread);
            preflight.Add(new Measurement
            {
                CrewMemberId = crew.Id, MetricCode = metric.Code, Value = Round(metric, value), RecordedAt = at,
                Source = HealthMeasurementSource.Preflight, SyncedAt = at,
            });
        }
        foreach (var m in preflight) yield return m;

        // Alex's BP drift is defined against the baseline these pre-flight readings will lock.
        var drifting = crewIndex == 0 && metric.Code is ("bp_sys" or "bp_dia");
        var baseline = drifting ? BaselineEngine.Compute(preflight.Select(m => m.Value).ToList()) : null;

        // In flight: mission days 1..90.
        for (var day = 1; day <= InflightDays; day++)
        {
            var noise = Normal(rng); // drawn every day so the stream stays aligned even on skipped days
            if (crewIndex == 3 && day >= MissedFromDay && day <= MissedUntilDay) continue;

            var at = Day(launch, day, ReadingTime[metric.Code]);
            var value = metric.Code switch
            {
                "mood" => MoodMean + noise * MoodSd,
                "exercise_min" => ExerciseMeanMin + noise * ExerciseSdMin,
                _ when baseline is { Sd: > 0 } b => b.Mean + (BpDriftSd(day) + noise * BpDriftNoiseFactor) * b.Sd,
                _ => center + noise * spread * InflightNoiseFactor + Adaptation(metric.Code, day, spread),
            };
            value += Planted(crewIndex, metric.Code, day, spread);
            if (crewIndex == 1 && metric.Code == "mood" && day >= MoodDipFromDay && day <= MoodDipUntilDay)
                value = day == 40 ? 1 : 2;
            if (crewIndex == 2 && day == FeverDay && metric.Code == "temp") value = FeverTempC;

            yield return new Measurement
            {
                CrewMemberId = crew.Id, MetricCode = metric.Code, Value = Round(metric, Clamp(metric, value)), RecordedAt = at,
                Source = HealthMeasurementSource.Inflight, SyncedAt = at + latency,
            };
        }
    }

    // Piecewise-linear ramp through BpDriftKnots (SDs above Alex's pre-flight baseline on a mission day).
    public static double BpDriftSd(int day)
    {
        var k = BpDriftKnots;
        if (day <= k[0].Day) return k[0].Sd;
        for (var i = 1; i < k.Length; i++)
            if (day <= k[i].Day)
                return k[i - 1].Sd + (k[i].Sd - k[i - 1].Sd) * (day - k[i - 1].Day) / (double)(k[i].Day - k[i - 1].Day);
        return k[^1].Sd;
    }

    // Gentle adaptation in the first 2 weeks (fluid shift, sleep changes), fading to zero by day 14.
    private static double Adaptation(string metric, int day, double spread)
    {
        if (day > 14) return 0;
        var weight = (14.0 - day) / 13.0;
        return metric switch
        {
            "hr_rest" => 0.5 * spread * weight,
            "sleep_h" => -0.5 * spread * weight,
            _ => 0,
        };
    }

    private static double Planted(int crewIndex, string metric, int day, double spread)
    {
        switch (crewIndex)
        {
            case 1 when metric == "sleep_h" && day >= SleepDeclineStartDay && day <= SleepLowUntilDay:
                // Linear decline to the low level by day 36, then stays low until day 45 (relative to ~8 h).
                var progress = Math.Min(1.0, (day - SleepDeclineStartDay) / (double)(SleepLowFromDay - SleepDeclineStartDay));
                return -progress * (8.0 - SleepLowHours);
            case 2 when metric == "hr_rest" && day == FeverDay:
                return FeverHrRise;
            default:
                return 0;
        }
    }

    // Personal mean near the middle of the normal band; day-to-day spread one eighth of that band.
    // Context inputs (mood, exercise) are modelled directly with the story parameters above.
    private static (double Center, double Spread) PersonalModel(HealthMetric m, Random rng)
    {
        if (m.Code == "mood") return (MoodMean, MoodSd);
        if (m.Code == "exercise_min") return (ExerciseMeanMin, ExerciseSdMin);
        var low = m.NormalLow ?? m.PlausibleMin;
        var high = m.NormalHigh ?? m.PlausibleMax;
        var width = high - low;
        return (low + width * (0.4 + 0.2 * rng.NextDouble()), width / 8);
    }

    private static double Clamp(HealthMetric m, double v) => Math.Clamp(v, m.PlausibleMin, m.PlausibleMax);

    // Integer readings unless the metric's own limits use decimals; hours of sleep are always decimal.
    private static double Round(HealthMetric m, double v) =>
        m.Unit != "h" && new[] { m.NormalLow, m.NormalHigh, m.WarnLow, m.WarnHigh, m.PlausibleMin, m.PlausibleMax }
            .All(x => x is null || x == Math.Floor(x.Value))
            ? Math.Round(v)
            : Math.Round(v, 1);

    private void AddSeededAlerts(Mission mission, CrewMember[] crew, List<Measurement> readings, List<EnvironmentLog> environment)
    {
        var baselines = db.Baselines.Local.Where(b => b.LockedAt != null)
            .ToDictionary(b => (b.CrewMemberId, b.MetricCode), b => new BaselineSnapshot(b.Mean, b.StandardDeviation, b.SampleSize));
        var env = environment.Select(e => new ContextEnvironmentEntry(e.Kind, e.Value, e.RecordedAt)).ToList();

        foreach (var member in crew)
        {
            var mine = readings.Where(r => r.CrewMemberId == member.Id).OrderBy(r => r.RecordedAt).ToList();
            var history = mine.Select(r => new ContextReading(r.MetricCode, r.Value, r.RecordedAt)).ToList();
            foreach (var r in mine.Where(r => r.Source == HealthMeasurementSource.Inflight))
            {
                var metric = config.Metrics[r.MetricCode];
                if (!metric.HasStatus) continue;
                var status = StatusEngine.Evaluate(metric, r.Value, baselines.GetValueOrDefault((member.Id, metric.Code)));
                if (status.Level == StatusLevel.Green) continue;

                ContextResult? context = null;
                if (config.ContextRules is { } rules)
                {
                    var upTo = history.Where(h => h.RecordedAt <= r.RecordedAt).ToList();
                    context = ContextEngine.Evaluate(rules, metric.Code, status.Level, status.Direction, r.RecordedAt, mission.LaunchDate, upTo, env);
                }

                var alert = new Alert
                {
                    CrewMemberId = member.Id, MeasurementId = r.Id, Level = status.Level,
                    Reason = $"{metric.Name} {r.Value} {metric.Unit}: {status.Reason}", RuleId = status.Rule,
                    CreatedAt = r.RecordedAt,
                    ContextJson = context is null ? null : JsonSerializer.Serialize(context, MissionService.Json),
                };
                db.Alerts.Add(alert);
                var payload = JsonSerializer.Serialize(new
                {
                    alertId = alert.Id, crewMemberId = member.Id, crewName = member.Name, metricCode = metric.Code,
                    value = r.Value, unit = metric.Unit, level = status.Level, rule = status.Rule, reason = status.Reason,
                    z = status.Z, baselineN = status.BaselineN, recordedAt = r.RecordedAt, context, protocolId = (string?)null,
                }, MissionService.Json);
                db.LinkMessages.Add(LinkQueue.Create(mission, r.RecordedAt, LinkDirection.CrewToGround, LinkMessageType.Alert, payload, alert.Id));
            }
        }
    }
}
