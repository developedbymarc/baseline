using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Engines;
using backend.Services.Link;
using backend.Services.Reminders;

namespace backend.Services.Missions;

public sealed class ApiProblem(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

// Which protocol the app offers to open, and what in its trigger matched (story E1).
public sealed record ProtocolOffer(
    string ProtocolId,
    // "red_reading" (no symptom needed), "red_reading+symptom", "symptom" or "symptom+red_reading".
    string MatchedBy,
    IReadOnlyList<Guid> MatchedSymptomLogIds,
    IReadOnlyList<Guid> MatchedRedAlertIds);

public sealed record SubmitMeasurementResult(
    Measurement? Measurement,
    PlausibilityResult Plausibility,
    StatusResult? Status,
    ContextResult? Context,
    Guid? AlertId,
    ProtocolOffer? Protocol,
    bool NeedsConfirmation);

public sealed record LogSymptomResult(Guid SymptomLogId, Guid? AlertId, ProtocolOffer? Protocol);

// The demo runs a single mission; "current mission" is the only one in the DB.
public sealed class MissionService(ApplicationDbContext db, HealthConfig config, MissionClockService clock)
{
    // Same JSON shape as the API responses (camelCase, enums as names) for everything stored or sent over the link.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task<Mission> CurrentAsync(CancellationToken ct = default) =>
        await db.Missions.OrderBy(m => m.LaunchDate).FirstOrDefaultAsync(ct)
        ?? throw new ApiProblem(404, "NO_MISSION", "No mission loaded. Run the seed (dotnet run -- reseed).");

    public DateTimeOffset Now(Mission m) => clock.Now(m);

    public async Task<CrewMember> CrewAsync(Mission m, Guid crewId, CancellationToken ct = default) =>
        await db.CrewMembers.FirstOrDefaultAsync(c => c.Id == crewId && c.MissionId == m.Id, ct)
        ?? throw new ApiProblem(404, "NO_CREW", $"Crew member {crewId} not found.");

    // ---------- Baselines (A4) ----------

    public async Task<Dictionary<string, BaselineSnapshot>> BaselineSnapshotsAsync(Guid crewId, CancellationToken ct = default) =>
        await db.Baselines.Where(b => b.CrewMemberId == crewId && b.LockedAt != null)
            .ToDictionaryAsync(b => b.MetricCode, b => new BaselineSnapshot(b.Mean, b.StandardDeviation, b.SampleSize), ct);

    // "Start mission": compute every baseline from pre-flight data, lock it, switch to in-flight.
    public async Task<List<Baseline>> StartMissionAsync(CancellationToken ct = default)
    {
        var mission = await CurrentAsync(ct);
        if (mission.Mode == MissionMode.Inflight)
            throw new ApiProblem(409, "ALREADY_INFLIGHT", "The mission has already started; baselines are locked.");

        var now = Now(mission);
        var crewIds = await db.CrewMembers.Where(c => c.MissionId == mission.Id).Select(c => c.Id).ToListAsync(ct);
        var preflight = await db.Measurements
            .Where(x => crewIds.Contains(x.CrewMemberId) && x.Source == HealthMeasurementSource.Preflight)
            .Select(x => new { x.CrewMemberId, x.MetricCode, x.Value })
            .ToListAsync(ct);

        var created = new List<Baseline>();
        foreach (var group in preflight.GroupBy(x => (x.CrewMemberId, x.MetricCode)))
        {
            if (!config.Metrics.TryGetValue(group.Key.MetricCode, out var metric) || !metric.HasStatus) continue;
            var stats = BaselineEngine.Compute(group.Select(x => x.Value).ToList());
            if (stats is null) continue;
            created.Add(new Baseline
            {
                CrewMemberId = group.Key.CrewMemberId,
                MetricCode = group.Key.MetricCode,
                Mean = stats.Mean,
                StandardDeviation = stats.Sd,
                SampleSize = stats.N,
                LockedAt = now,
            });
        }

        db.Baselines.AddRange(created);
        mission.Mode = MissionMode.Inflight;
        await db.SaveChangesAsync(ct);
        return created;
    }

    // ---------- Measurements (C2, C3, E1, E2) ----------

    public async Task<SubmitMeasurementResult> SubmitMeasurementAsync(
        Guid crewId, string metricCode, double value, bool confirmImplausible, string? note, CancellationToken ct = default)
    {
        var mission = await CurrentAsync(ct);
        var crew = await CrewAsync(mission, crewId, ct);
        if (!config.Metrics.TryGetValue(metricCode, out var metric))
            throw new ApiProblem(400, "UNKNOWN_METRIC", $"Unknown metric '{metricCode}'.");

        var plausibility = StatusEngine.CheckPlausible(metric, value);
        if (!plausibility.Plausible && !confirmImplausible)
            return new SubmitMeasurementResult(null, plausibility, null, null, null, null, NeedsConfirmation: true);

        var now = Now(mission);
        var inflight = mission.Mode == MissionMode.Inflight;
        var measurement = new Measurement
        {
            CrewMemberId = crew.Id,
            MetricCode = metric.Code,
            Value = value,
            RecordedAt = now,
            Source = inflight ? HealthMeasurementSource.Inflight : HealthMeasurementSource.Preflight,
            QualityFlag = plausibility.Plausible ? QualityFlag.Ok : QualityFlag.ImplausibleConfirmed,
            Note = note,
        };
        db.Measurements.Add(measurement);

        var baselines = await BaselineSnapshotsAsync(crew.Id, ct);
        var status = StatusEngine.Evaluate(metric, value, baselines.GetValueOrDefault(metric.Code));

        Guid? alertId = null;
        ContextResult? context = null;
        ProtocolOffer? offer = null;
        if (inflight && status.Level != StatusLevel.Green)
        {
            context = await EvaluateContextAsync(mission, crew.Id, metric.Code, status, measurement, ct);
            var alert = new Alert
            {
                CrewMemberId = crew.Id,
                MeasurementId = measurement.Id,
                // The original flag is what gets logged; a downgrade is recorded in ContextJson (I4: never hidden).
                Level = status.Level,
                Reason = $"{metric.Name} {value} {metric.Unit}: {status.Reason}",
                RuleId = status.Rule,
                CreatedAt = now,
                ContextJson = context is null ? null : JsonSerializer.Serialize(context, Json),
            };
            db.Alerts.Add(alert);
            alertId = alert.Id;

            if (status.Level == StatusLevel.Red)
                offer = await OfferForRedReadingAsync(crew.Id, metric.Code, now, ct);

            // E2: ground learns about the alert only through the delayed link.
            var payload = JsonSerializer.Serialize(new
            {
                alertId = alert.Id,
                crewMemberId = crew.Id,
                crewName = crew.Name,
                metricCode = metric.Code,
                value,
                unit = metric.Unit,
                level = status.Level.ToString(),
                rule = status.Rule,
                reason = status.Reason,
                z = status.Z,
                baselineN = status.BaselineN,
                recordedAt = now,
                context,
                protocolId = offer?.ProtocolId,
            }, Json);
            db.LinkMessages.Add(LinkQueue.Create(mission, now, LinkDirection.CrewToGround, LinkMessageType.Alert, payload, alert.Id));
        }

        if (inflight)
        {
            await SyncRemindersAsync(mission, crew.Id, ct);
            var open = await db.ReminderInstances
                .Where(i => i.Rule.CrewMemberId == crew.Id && i.Rule.MetricCode == metric.Code && i.Status == ReminderStatus.Pending)
                .ToListAsync(ct);
            ReminderRules.Complete(open, now);
        }

        await db.SaveChangesAsync(ct);
        return new SubmitMeasurementResult(measurement, plausibility, status, context, alertId, offer, NeedsConfirmation: false);
    }

    // ---------- Context factors (WP 4.3, I1-I4) ----------

    private async Task<ContextResult?> EvaluateContextAsync(
        Mission mission, Guid crewId, string metricCode, StatusResult status, Measurement current, CancellationToken ct)
    {
        if (config.ContextRules is not { } rules) return null;
        var at = current.RecordedAt;
        var from = at - ContextEngine.MaxLookback(rules);

        var readings = await db.Measurements
            .Where(x => x.CrewMemberId == crewId && x.RecordedAt >= from && x.RecordedAt <= at)
            .Select(x => new ContextReading(x.MetricCode, x.Value, x.RecordedAt))
            .ToListAsync(ct);
        readings.Add(new ContextReading(current.MetricCode, current.Value, current.RecordedAt)); // not saved yet

        var environment = await db.EnvironmentLogs
            .Where(e => e.MissionId == mission.Id && e.RecordedAt >= from && e.RecordedAt <= at)
            .Select(e => new ContextEnvironmentEntry(e.Kind, e.Value, e.RecordedAt))
            .ToListAsync(ct);

        return ContextEngine.Evaluate(rules, metricCode, status.Level, status.Direction, at, mission.LaunchDate, readings, environment);
    }

    // ---------- Protocol triggers (E1, C6) ----------

    private static bool InTriggerWindow(DateTimeOffset a, DateTimeOffset b, int windowMin) =>
        (a - b).Duration() <= TimeSpan.FromMinutes(windowMin);

    // A red reading on a trigger.red_metrics metric opens the protocol on its own, or, with
    // requires_symptom, only if a trigger symptom was logged within window_min of mission time (either side).
    private async Task<ProtocolOffer?> OfferForRedReadingAsync(Guid crewId, string metricCode, DateTimeOffset at, CancellationToken ct)
    {
        foreach (var p in config.Protocols.Values)
        {
            if (p.Trigger is not { } t || !t.RedMetrics.Contains(metricCode)) continue;
            if (!t.RequiresSymptom) return new ProtocolOffer(p.Id, "red_reading", [], []);

            var window = TimeSpan.FromMinutes(t.WindowMin!.Value);
            var (lo, hi) = (at - window, at + window);
            var symptoms = await db.SymptomLogs
                .Where(s => s.CrewMemberId == crewId && t.Symptoms.Contains(s.SymptomCode) && s.LoggedAt >= lo && s.LoggedAt <= hi)
                .Select(s => s.Id)
                .ToListAsync(ct);
            if (symptoms.Count > 0) return new ProtocolOffer(p.Id, "red_reading+symptom", symptoms, []);
        }
        return null;
    }

    // C6 / demo step 5: a trigger symptom raises a red alert and offers its protocol. Any red reading on the
    // protocol's red_metrics within window_min (either side) is reported as matched, so ground sees both.
    public async Task<LogSymptomResult> LogSymptomAsync(Guid crewId, string symptomCode, string? note, CancellationToken ct = default)
    {
        var mission = await CurrentAsync(ct);
        var crew = await CrewAsync(mission, crewId, ct);
        var now = Now(mission);
        var log = new SymptomLog { CrewMemberId = crew.Id, SymptomCode = symptomCode, LoggedAt = now, Note = note };
        db.SymptomLogs.Add(log);

        Guid? alertId = null;
        ProtocolOffer? offer = null;
        if (config.ProtocolForSymptom(symptomCode) is { } protocol)
        {
            var t = protocol.Trigger!;
            var redAlerts = new List<Guid>();
            if (t.RedMetrics.Count > 0 && t.WindowMin is { } wm)
            {
                var window = TimeSpan.FromMinutes(wm);
                var (lo, hi) = (now - window, now + window);
                redAlerts = await db.Alerts
                    .Where(a => a.CrewMemberId == crew.Id && a.Level == StatusLevel.Red && a.MeasurementId != null
                                && a.CreatedAt >= lo && a.CreatedAt <= hi
                                && t.RedMetrics.Contains(a.Measurement!.MetricCode))
                    .Select(a => a.Id)
                    .ToListAsync(ct);
            }
            offer = new ProtocolOffer(protocol.Id, redAlerts.Count > 0 ? "symptom+red_reading" : "symptom", [log.Id], redAlerts);

            var alert = new Alert
            {
                CrewMemberId = crew.Id,
                Level = StatusLevel.Red,
                Reason = $"Symptom reported: {protocol.Title}.",
                RuleId = "symptom",
                CreatedAt = now,
            };
            db.Alerts.Add(alert);
            alertId = alert.Id;
            var payload = JsonSerializer.Serialize(new
            {
                alertId = alert.Id, crewMemberId = crew.Id, crewName = crew.Name, symptomCode,
                level = "Red", reason = alert.Reason, note, recordedAt = now,
                protocolId = protocol.Id, matchedRedAlertIds = redAlerts,
            }, Json);
            db.LinkMessages.Add(LinkQueue.Create(mission, now, LinkDirection.CrewToGround, LinkMessageType.Alert, payload, alert.Id));
        }

        await db.SaveChangesAsync(ct);
        return new LogSymptomResult(log.Id, alertId, offer);
    }

    // ---------- Reminders (B2, B3) ----------

    private static readonly TimeSpan ReminderLookahead = TimeSpan.FromDays(1);

    // Creates reminder instances up to a day ahead of mission time and marks missed ones. Idempotent.
    public async Task SyncRemindersAsync(Mission mission, Guid crewId, CancellationToken ct = default)
    {
        var now = Now(mission);
        var rules = await db.ReminderRules.Where(r => r.CrewMemberId == crewId).ToListAsync(ct);
        foreach (var rule in rules)
        {
            var schedule = ReminderSchedule.Parse(rule.Schedule);
            var existing = await db.ReminderInstances.Where(i => i.RuleId == rule.Id).ToListAsync(ct);
            var known = existing.Select(i => i.DueAt).ToHashSet();
            var from = existing.Count > 0 ? existing.Max(i => i.DueAt) : mission.LaunchDate;
            foreach (var due in schedule.Occurrences(mission.LaunchDate, from, now + ReminderLookahead))
            {
                if (known.Contains(due)) continue;
                var inst = new ReminderInstance { RuleId = rule.Id, DueAt = due };
                db.ReminderInstances.Add(inst);
                existing.Add(inst);
            }
            ReminderRules.MarkMissed(existing.OrderBy(i => i.DueAt).ToList(), now, schedule.Kind == "one_off");
        }
        await db.SaveChangesAsync(ct);
    }

    // ---------- Link (G3, H2) ----------

    public async Task<List<LinkMessage>> DeliverDueAsync(Mission mission, CancellationToken ct = default)
    {
        var now = Now(mission);
        var pending = await db.LinkMessages
            .Where(m => m.MissionId == mission.Id && m.DeliveredAt == null && m.DeliverAt != null && m.DeliverAt <= now)
            .ToListAsync(ct);
        var delivered = LinkQueue.DeliverDue(pending, now);
        await db.SaveChangesAsync(ct);
        return delivered;
    }

    public async Task SetLinkAsync(Mission mission, bool? up, double? latencySeconds, CancellationToken ct = default)
    {
        if (latencySeconds is < 0) throw new ApiProblem(400, "BAD_LATENCY", "Latency cannot be negative.");
        await DeliverDueAsync(mission, ct);
        if (latencySeconds is { } l) mission.OneWayLatencySeconds = l;
        if (up is { } u)
        {
            var held = await db.LinkMessages.Where(m => m.MissionId == mission.Id && m.DeliverAt == null).ToListAsync(ct);
            LinkQueue.SetLink(mission, u, Now(mission), held);
        }
        await db.SaveChangesAsync(ct);
    }

    // H1/H2: the handoff packet is a LinkMessage payload (type = packet) linked to its alert.
    public async Task<LinkMessage> QueuePacketAsync(Guid crewId, Guid? alertId, JsonElement packet, string? note, CancellationToken ct = default)
    {
        var mission = await CurrentAsync(ct);
        var crew = await CrewAsync(mission, crewId, ct);
        if (alertId is { } a && !await db.Alerts.AnyAsync(x => x.Id == a && x.CrewMemberId == crew.Id, ct))
            throw new ApiProblem(404, "NO_ALERT", $"Alert {a} not found for this crew member.");

        var now = Now(mission);
        var payload = JsonSerializer.Serialize(new { crewMemberId = crew.Id, crewName = crew.Name, createdAt = now, note, packet }, Json);
        var msg = LinkQueue.Create(mission, now, LinkDirection.CrewToGround, LinkMessageType.Packet, payload, alertId);
        db.LinkMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        return msg;
    }
}
