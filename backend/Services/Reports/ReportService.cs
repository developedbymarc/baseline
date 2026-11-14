using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Engines;
using backend.Services.Missions;
using backend.Services.Reminders;

namespace backend.Services.Reports;

public sealed record ReportPeriod(ReportWindow Window, int Number, DateTimeOffset Start, DateTimeOffset End, int StartDay, int EndDay, bool Complete);

public sealed record AlertCounts(int MeasurementAlerts, int WithContextFactors, int WithoutContextFactors, int SymptomAlerts, IReadOnlyList<Guid> UnexplainedAlertIds);

public sealed record MemberReport(
    Guid CrewMemberId, string Name, string Role, IReadOnlyList<MetricRow> Rows, int MissedTotal, AlertCounts Alerts, IReadOnlyList<string> Highlights);

public sealed record AdaptationInfo(int Days, bool Overlaps, string? Note);

public sealed record ReportDto(
    ReportScope Scope,
    ReportWindow Window,
    int Period,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int StartDay,
    int EndDay,
    bool Complete,
    // "crew" = all crew-side data; "ground" = only data delivered through the link by DataAsOf.
    string View,
    DateTimeOffset DataAsOf,
    string DataAsOfMet,
    AdaptationInfo AdaptationWindow,
    ReportOptions Parameters,
    IReadOnlyList<MemberReport> Members,
    AlertCounts AlertsTotal,
    IReadOnlyList<string> Highlights);

// Biweekly (14 d) and monthly (30 d) reports, fixed periods from launch (WP 4.4, D3-D4, I5).
public sealed class ReportService(
    ApplicationDbContext db, HealthConfig config, MissionClockService clock, MissionService missions, IOptions<ReportOptions> opts)
{
    private readonly ReportOptions _options = opts.Value;

    public static int Days(ReportWindow w) => w == ReportWindow.Biweekly ? 14 : 30;

    public int AdaptationDays =>
        (int?)config.ContextRules?.Rules.FirstOrDefault(r => r.Id == "adaptation_window")?.Condition?.Value
        ?? _options.AdaptationWindowDaysFallback;

    public static ReportPeriod Period(Mission m, ReportWindow w, int number, DateTimeOffset now)
    {
        if (number < 1) throw new ApiProblem(400, "BAD_PERIOD", "Periods are numbered from 1.");
        var days = Days(w);
        var start = m.LaunchDate.AddDays((number - 1) * days);
        var end = start.AddDays(days);
        return new ReportPeriod(w, number, start, end, (number - 1) * days + 1, number * days, now >= end);
    }

    // Periods from launch up to the one containing mission time now.
    public List<ReportPeriod> Periods(Mission m, ReportWindow w, DateTimeOffset now)
    {
        var list = new List<ReportPeriod>();
        if (now < m.LaunchDate) return list;
        var current = (int)Math.Floor((now - m.LaunchDate).TotalDays / Days(w)) + 1;
        for (var k = 1; k <= current; k++) list.Add(Period(m, w, k, now));
        return list;
    }

    public int ResolvePeriod(Mission m, ReportWindow w, string period, DateTimeOffset now)
    {
        if (period.Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            if (now < m.LaunchDate) throw new ApiProblem(404, "NO_PERIOD", "The mission has not launched yet.");
            return (int)Math.Floor((now - m.LaunchDate).TotalDays / Days(w)) + 1;
        }
        return int.TryParse(period, out var k) ? k : throw new ApiProblem(400, "BAD_PERIOD", "Period must be a number or 'current'.");
    }

    public async Task<ReportDto> BuildAsync(Guid? crewId, ReportWindow window, int number, bool groundView, CancellationToken ct = default)
    {
        var mission = await missions.CurrentAsync(ct);
        if (groundView) await missions.DeliverDueAsync(mission, ct);
        var now = clock.Now(mission);
        var period = Period(mission, window, number, now);
        if (period.Start > now) throw new ApiProblem(404, "NO_PERIOD", $"Period {number} has not started yet.");

        var crewQuery = db.CrewMembers.Where(c => c.MissionId == mission.Id);
        if (crewId is { } cid) crewQuery = crewQuery.Where(c => c.Id == cid);
        var crew = await crewQuery.OrderBy(c => c.Name).ToListAsync(ct);
        if (crewId is not null && crew.Count == 0) throw new ApiProblem(404, "NO_CREW", $"Crew member {crewId} not found.");

        var members = new List<MemberReport>();
        foreach (var c in crew) members.Add(await MemberAsync(mission, c, period, now, groundView, ct));

        var total = new AlertCounts(
            members.Sum(m => m.Alerts.MeasurementAlerts), members.Sum(m => m.Alerts.WithContextFactors),
            members.Sum(m => m.Alerts.WithoutContextFactors), members.Sum(m => m.Alerts.SymptomAlerts),
            members.SelectMany(m => m.Alerts.UnexplainedAlertIds).ToList());

        var adaptation = AdaptationDays;
        var overlaps = period.StartDay <= adaptation;
        var adaptationInfo = new AdaptationInfo(adaptation, overlaps,
            overlaps ? $"This period includes the adaptation window (mission days 1-{adaptation}); early shifts are expected and drift alerts are not raised for them." : null);

        var highlights = new List<string>();
        if (adaptationInfo.Note is { } note) highlights.Add(note);
        if (crewId is null)
            foreach (var m in members)
                highlights.AddRange(m.Highlights.Where(h => !h.StartsWith("Alerts:")).Select(h => $"{m.Name}: {h}"));
        else
            highlights.AddRange(members.Single().Highlights);
        highlights.Add(AlertLine(total, crewId is null ? "Across the crew" : "Alerts"));
        if (groundView)
            highlights.Add($"Ground view: data delivered by {MissionClock.FormatMet(MissionClock.Met(mission, now))} MET; readings still in transit are not included.");
        if (!period.Complete)
            highlights.Add($"Period in progress: mission days {period.StartDay}-{period.EndDay}, data up to now.");

        return new ReportDto(crewId is null ? ReportScope.Crew : ReportScope.Member, window, number, period.Start, period.End,
            period.StartDay, period.EndDay, period.Complete, groundView ? "ground" : "crew", now,
            MissionClock.FormatMet(MissionClock.Met(mission, now)), adaptationInfo, _options, members, total, highlights);
    }

    private static string AlertLine(AlertCounts a, string label) =>
        a.MeasurementAlerts == 0 && a.SymptomAlerts == 0
            ? $"{label}: no alerts in this period."
            : $"{label}: {a.MeasurementAlerts} reading alerts, {a.WithContextFactors} with context factors and {a.WithoutContextFactors} without (unexplained); {a.SymptomAlerts} symptom alerts.";

    private async Task<MemberReport> MemberAsync(Mission mission, CrewMember crew, ReportPeriod period, DateTimeOffset now, bool groundView, CancellationToken ct)
    {
        var until = now < period.End ? now : period.End;
        var source = groundView ? missions.GroundVisibleMeasurements(now) : db.Measurements;
        // Readings for the window, plus a margin around it for the reminder completion windows.
        var marginFrom = period.Start - ReminderRules.EarlyWindow;
        var readings = await source
            .Where(x => x.CrewMemberId == crew.Id && x.RecordedAt >= marginFrom && x.RecordedAt <= until.AddDays(30))
            .Select(x => new { x.MetricCode, x.Value, x.RecordedAt, x.Source })
            .ToListAsync(ct);
        var preflight = await db.Measurements
            .Where(x => x.CrewMemberId == crew.Id && x.Source == HealthMeasurementSource.Preflight)
            .Select(x => new { x.MetricCode, x.Value })
            .ToListAsync(ct);
        var locked = await db.Baselines.Where(b => b.CrewMemberId == crew.Id && b.LockedAt != null).ToListAsync(ct);
        var rules = await db.ReminderRules.Where(r => r.CrewMemberId == crew.Id).ToListAsync(ct);

        var rows = new List<MetricRow>();
        foreach (var metric in config.Metrics.Values)
        {
            var window = readings
                .Where(r => r.MetricCode == metric.Code && r.Source == HealthMeasurementSource.Inflight
                            && r.RecordedAt >= period.Start && r.RecordedAt < until.AddTicks(1) && r.RecordedAt < period.End)
                .Select(r => new ReportReading(r.Value, r.RecordedAt)).ToList();

            BaselineSnapshot? baseline;
            string source2;
            var b = locked.FirstOrDefault(x => x.MetricCode == metric.Code);
            if (b is not null) { baseline = new BaselineSnapshot(b.Mean, b.StandardDeviation, b.SampleSize); source2 = "locked"; }
            else
            {
                var stats = BaselineEngine.Compute(preflight.Where(p => p.MetricCode == metric.Code).Select(p => p.Value).ToList());
                baseline = stats is null ? null : new BaselineSnapshot(stats.Mean, stats.Sd, stats.N);
                source2 = "preflight";
            }

            var (missed, expected) = (0, 0);
            foreach (var rule in rules.Where(r => r.MetricCode == metric.Code))
            {
                var schedule = ReminderSchedule.Parse(rule.Schedule);
                var dues = schedule.Occurrences(mission.LaunchDate, period.Start, period.End.AddTicks(-1)).ToList();
                var interval = schedule.Kind switch
                {
                    "daily" => TimeSpan.FromDays(1),
                    "weekly" => TimeSpan.FromDays(7),
                    "every_n_days" => TimeSpan.FromDays(schedule.N ?? 1),
                    _ => ReminderRules.OneOffMissedAfter,
                };
                var times = readings.Where(r => r.MetricCode == metric.Code).Select(r => r.RecordedAt).ToList();
                var (m, e) = ReportEngine.CountMissed(dues, times, now, ReminderRules.EarlyWindow, interval);
                missed += m;
                expected += e;
            }

            rows.Add(ReportEngine.BuildRow(metric, window, baseline, source2, period.Start, ReportService.Days(period.Window),
                missed, expected, _options));
        }

        // I5: which alerts had context factors and which had none.
        var alertSource = groundView ? missions.GroundVisibleAlerts() : db.Alerts;
        var alerts = await alertSource
            .Where(a => a.CrewMemberId == crew.Id && a.CreatedAt >= period.Start && a.CreatedAt < period.End && a.CreatedAt <= now)
            .Select(a => new { a.Id, a.RuleId, a.ContextJson })
            .ToListAsync(ct);
        var readingAlerts = alerts.Where(a => a.RuleId != "symptom").ToList();
        var withFactors = readingAlerts.Where(a => HasFactors(a.ContextJson)).ToList();
        var unexplained = readingAlerts.Where(a => !HasFactors(a.ContextJson)).Select(a => a.Id).ToList();
        var counts = new AlertCounts(readingAlerts.Count, withFactors.Count, unexplained.Count, alerts.Count - readingAlerts.Count, unexplained);

        var highlights = ReportEngine.Highlights(rows, code => config.Metrics.GetValueOrDefault(code)?.ZWarn);
        highlights.Add(AlertLine(counts, "Alerts"));
        return new MemberReport(crew.Id, crew.Name, crew.Role, rows, rows.Sum(r => r.Missed), counts, highlights);
    }

    private static bool HasFactors(string? contextJson)
    {
        if (contextJson is null) return false;
        using var doc = JsonDocument.Parse(contextJson);
        return doc.RootElement.TryGetProperty("factors", out var f) && f.GetArrayLength() > 0;
    }

    // "Auto-generates": every completed period is stored in REPORT (crew view = all crew-side data),
    // per member and for the whole crew, the first time reports are requested after it ends.
    public async Task<int> StoreCompletedAsync(CancellationToken ct = default)
    {
        var mission = await missions.CurrentAsync(ct);
        var now = clock.Now(mission);
        var crewIds = await db.CrewMembers.Where(c => c.MissionId == mission.Id).Select(c => c.Id).ToListAsync(ct);
        var stored = 0;
        foreach (var window in new[] { ReportWindow.Biweekly, ReportWindow.Monthly })
        {
            foreach (var p in Periods(mission, window, now).Where(p => p.Complete))
            {
                foreach (var scope in crewIds.Select(id => (Guid?)id).Append(null))
                {
                    var exists = await db.Reports.AnyAsync(r => r.MissionId == mission.Id && r.Window == window
                                                                && r.PeriodStart == p.Start && r.CrewMemberId == scope, ct);
                    if (exists) continue;
                    var dto = await BuildAsync(scope, window, p.Number, groundView: false, ct);
                    db.Reports.Add(new Report
                    {
                        MissionId = mission.Id, CrewMemberId = scope, Scope = scope is null ? ReportScope.Crew : ReportScope.Member,
                        Window = window, PeriodStart = p.Start, PeriodEnd = p.End, GeneratedAt = now,
                        PayloadJson = JsonSerializer.Serialize(dto, MissionService.Json),
                    });
                    stored++;
                }
            }
        }
        await db.SaveChangesAsync(ct);
        return stored;
    }
}
