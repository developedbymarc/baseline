using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scriban;
using Scriban.Runtime;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Services.Clock;
using backend.Services.Engines;
using backend.Services.Missions;

namespace backend.Services.Packets;

// Parsed once at start from Templates/sbar_packet.sbn; a template error stops startup.
public sealed class PacketTemplate
{
    public Template Template { get; }

    public PacketTemplate(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Packet template not found: {path}");
        Template = Template.Parse(File.ReadAllText(path), path);
        if (Template.HasErrors)
            throw new InvalidOperationException("Packet template errors: " + string.Join("; ", Template.Messages));
    }

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Templates", "sbar_packet.sbn");

    public string Render(object model)
    {
        var globals = new ScriptObject();
        globals.Import(model);
        var context = new TemplateContext { StrictVariables = true };
        context.PushCulture(CultureInfo.InvariantCulture);
        context.PushGlobal(globals);
        return Normalize(Template.Render(context));
    }

    private static readonly HashSet<string> Sections = ["SITUATION", "BACKGROUND", "ASSESSMENT", "RECOMMENDATION"];

    // Template control tags leave blank lines; keep the text compact: no empty lines except one
    // before each SBAR heading. Deterministic, so the same data always gives the same text.
    internal static string Normalize(string rendered)
    {
        var lines = rendered.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0);
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (Sections.Contains(line) && sb.Length > 0) sb.Append('\n');
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }
}

// Builds the SBAR handoff packet from stored data only (H1): no typing needed except the optional note (H2).
public sealed class PacketBuilder(ApplicationDbContext db, HealthConfig config, MissionClockService clock, PacketTemplate template)
{
    public const string Schema = "baseline.sbar.v1";
    public static readonly TimeSpan Window = TimeSpan.FromHours(72);
    public const int MaxNoteLength = 500;

    public async Task<HandoffPacket> BuildAsync(Guid crewId, Guid? alertId, Guid? runId, string? note, CancellationToken ct = default)
    {
        if (alertId is null && runId is null)
            throw new ApiProblem(400, "NO_TRIGGER", "A packet needs an alertId or a runId.");
        if (note is { Length: > MaxNoteLength })
            throw new ApiProblem(400, "NOTE_TOO_LONG", $"The note can be at most {MaxNoteLength} characters.");
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var mission = await db.Missions.OrderBy(m => m.LaunchDate).FirstAsync(ct);
        var crew = await db.CrewMembers.FirstOrDefaultAsync(c => c.Id == crewId && c.MissionId == mission.Id, ct)
                   ?? throw new ApiProblem(404, "NO_CREW", $"Crew member {crewId} not found.");
        var now = clock.Now(mission);
        string Met(DateTimeOffset t) => MissionClock.FormatMet(MissionClock.Met(mission, t));

        ProtocolRun? run = null;
        if (runId is { } rid)
        {
            run = await db.ProtocolRuns.FirstOrDefaultAsync(r => r.Id == rid && r.CrewMemberId == crew.Id, ct)
                  ?? throw new ApiProblem(404, "NO_RUN", $"Protocol run {rid} not found for this crew member.");
            alertId ??= run.AlertId;
        }

        Alert? alert = null;
        Measurement? alertReading = null;
        if (alertId is { } aid)
        {
            alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == aid && a.CrewMemberId == crew.Id, ct)
                    ?? throw new ApiProblem(404, "NO_ALERT", $"Alert {aid} not found for this crew member.");
            if (alert.MeasurementId is { } mid) alertReading = await db.Measurements.FirstOrDefaultAsync(x => x.Id == mid, ct);
        }

        var protocol = run is null ? null : config.Protocols.GetValueOrDefault(run.ProtocolId);
        var from = now - Window;

        // Relevant metrics: the flagged metric plus the metrics of any protocol involved (trigger.red_metrics and
        // measure nodes) - from the run's protocol, or protocols whose trigger lists the flagged metric.
        var relevant = new List<string>();
        void AddMetric(string code) { if (config.Metrics.TryGetValue(code, out var m) && m.HasStatus && !relevant.Contains(code)) relevant.Add(code); }
        if (alertReading is not null) AddMetric(alertReading.MetricCode);
        var protocols = protocol is not null
            ? new[] { protocol }
            : config.Protocols.Values.Where(p => alertReading is not null && p.Trigger?.RedMetrics.Contains(alertReading.MetricCode) == true).ToArray();
        foreach (var p in protocols)
        {
            foreach (var code in p.Trigger?.RedMetrics ?? []) AddMetric(code);
            foreach (var node in p.TreeYaml.Where(n => n.Type == "measure")) foreach (var code in node.Metrics) AddMetric(code);
        }
        if (relevant.Count == 0) foreach (var m in config.Metrics.Values.Where(m => m.HasStatus)) AddMetric(m.Code);

        var recent = await db.Measurements
            .Where(x => x.CrewMemberId == crew.Id && x.RecordedAt >= from && x.RecordedAt <= now)
            .OrderBy(x => x.RecordedAt)
            .ToListAsync(ct);

        var baselineRows = await db.Baselines.Where(b => b.CrewMemberId == crew.Id && b.LockedAt != null).ToListAsync(ct);
        var snapshots = baselineRows.ToDictionary(b => b.MetricCode, b => new BaselineSnapshot(b.Mean, b.StandardDeviation, b.SampleSize));

        var measurements72h = recent.Where(x => relevant.Contains(x.MetricCode)).Select(x =>
        {
            var m = config.Metrics[x.MetricCode];
            var st = x.Source == HealthMeasurementSource.Inflight
                ? StatusEngine.Evaluate(m, x.Value, snapshots.GetValueOrDefault(x.MetricCode))
                : null;
            return new PacketMeasurement(x.MetricCode, m.Name, m.Unit, x.Value, x.RecordedAt, Met(x.RecordedAt), st?.Level, st?.Rule);
        }).ToList();

        var baselines = relevant.Where(snapshots.ContainsKey).Select(code =>
        {
            var m = config.Metrics[code];
            var b = baselineRows.Single(x => x.MetricCode == code);
            var sdEff = BaselineEngine.EffectiveSd(b.StandardDeviation, b.SampleSize, m.SdPop);
            var latest = recent.LastOrDefault(x => x.MetricCode == code);
            double? z = latest is not null && sdEff > 0 ? (latest.Value - b.Mean) / sdEff : null;
            var summary = $"{m.Name}: baseline {b.Mean.ToString("0.#", CultureInfo.InvariantCulture)} {m.Unit}, SD {sdEff.ToString("0.##", CultureInfo.InvariantCulture)}, n={b.SampleSize}"
                          + (latest is null ? "; no reading in the last 72 h"
                              : $"; latest {latest.Value.ToString("0.##", CultureInfo.InvariantCulture)} {m.Unit}"
                                + (z is { } zz ? $" ({(zz >= 0 ? "+" : "")}{zz.ToString("0.0", CultureInfo.InvariantCulture)} SD)" : ""));
            return new PacketBaseline(code, m.Name, m.Unit, b.Mean, sdEff, b.SampleSize, BaselineEngine.IsShrunk(b.SampleSize, m.SdPop),
                latest?.Value, latest?.RecordedAt, z, summary);
        }).ToList();

        // Symptoms: trigger symptoms of the protocols involved (or any symptom without a protocol) in the window.
        var symptomCodes = protocols.SelectMany(p => p.Trigger?.Symptoms ?? []).ToHashSet();
        var symptoms = await db.SymptomLogs
            .Where(s => s.CrewMemberId == crew.Id && s.LoggedAt >= from && s.LoggedAt <= now)
            .OrderBy(s => s.LoggedAt)
            .ToListAsync(ct);
        var packetSymptoms = symptoms.Where(s => symptomCodes.Count == 0 || symptomCodes.Contains(s.SymptomCode))
            .Select(s => new PacketSymptom(s.Id, s.SymptomCode, s.Note, s.LoggedAt, Met(s.LoggedAt))).ToList();

        // Context factors stored on the alert by the context engine.
        var context = alert?.ContextJson is { } cj ? JsonSerializer.Deserialize<ContextResult>(cj, MissionService.Json) : null;

        PacketProtocol? packetProtocol = null;
        if (run is not null)
        {
            var steps = JsonDocument.Parse(run.StepsJson).RootElement.EnumerateArray().Select(e =>
            {
                var nodeId = e.GetProperty("nodeId").GetString()!;
                var answer = e.TryGetProperty("answer", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
                var at = e.GetProperty("at").GetDateTimeOffset();
                var text = nodeId == "end" ? "Protocol ended" : protocol?.TreeYaml.FirstOrDefault(n => n.Id == nodeId)?.Text.Trim() ?? nodeId;
                return new PacketStep(nodeId, text, answer, at, Met(at));
            }).ToList();
            packetProtocol = new PacketProtocol(run.Id, run.ProtocolId, protocol?.Title ?? run.ProtocolId, run.ProtocolVersion,
                run.StartedAt, Met(run.StartedAt), run.EndedAt, steps);
        }

        var alertInfo = alert is null ? null : new PacketAlert(alert.Id, alert.Level, alert.Reason, alert.RuleId, alert.CreatedAt, Met(alert.CreatedAt),
            alertReading?.MetricCode, alertReading is null ? null : config.Metrics.GetValueOrDefault(alertReading.MetricCode)?.Name,
            alertReading?.Value, alertReading is null ? null : config.Metrics.GetValueOrDefault(alertReading.MetricCode)?.Unit);

        var (kind, summary) = TriggerSummary(alertInfo, packetSymptoms, packetProtocol);
        var missionDay = ContextEngine.MissionDay(mission.LaunchDate, now);
        var adaptationDays = config.ContextRules?.Rules.FirstOrDefault(r => r.Id == "adaptation_window")?.Condition?.Value;

        var data = new PacketData(
            new PacketMission(mission.Name, mission.Environment, mission.OneWayLatencySeconds,
                MissionClock.FormatAge(TimeSpan.FromSeconds(mission.OneWayLatencySeconds)), mission.LinkUp, missionDay,
                adaptationDays is { } ad && missionDay >= 1 && missionDay <= ad),
            new PacketCrew(crew.Id, crew.Name, crew.Role, crew.Age, crew.Sex),
            new PacketTrigger(kind, summary, alertInfo, packetSymptoms),
            measurements72h,
            baselines,
            packetProtocol,
            context?.Factors ?? [],
            context is null ? null : new PacketContextOutcome(context.OriginalLevel, context.Level, context.Downgraded, context.DowngradedBy),
            await FollowUpAsync(mission, crew.Id, now, packetSymptoms, alertInfo, packetProtocol, protocol, recent, Met, ct),
            Recommendation(packetProtocol, alertInfo, mission));

        var packet = new HandoffPacket(Schema, now, Met(now), now.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
            crew.Id, crew.Name, alert?.Id, run?.Id, note, data, "");
        return packet with { Text = template.Render(packet) };
    }

    private static (string Kind, string Summary) TriggerSummary(PacketAlert? alert, List<PacketSymptom> symptoms, PacketProtocol? protocol)
    {
        if (alert?.MetricCode is not null)
            return ("red_reading", $"{alert.Level} {alert.MetricName} {alert.Value?.ToString("0.##", CultureInfo.InvariantCulture)} {alert.Unit} at {alert.AtMet} MET.");
        if (symptoms.Count > 0)
            return ("symptom", $"Symptom reported: {string.Join(", ", symptoms.Select(s => s.SymptomCode).Distinct())}, first at {symptoms[0].LoggedMet} MET.");
        if (protocol is not null)
            return ("protocol", $"{protocol.Title} started at {protocol.StartedMet} MET.");
        return ("alert", alert?.Reason ?? "Handoff requested.");
    }

    private static string Recommendation(PacketProtocol? protocol, PacketAlert? alert, Mission mission)
    {
        var parts = new List<string>();
        if (protocol is not null)
        {
            var last = protocol.Steps.LastOrDefault();
            parts.Add(protocol.EndedAt is null
                ? $"On-board protocol in progress ({protocol.Title}); last step: {(last?.Text ?? "started").TrimEnd('.', '?', '!', ' ')}."
                : $"On-board protocol completed ({protocol.Title}).");
        }
        else if (alert is not null)
            parts.Add($"{alert.Level} flag on board; crew is following the app's next-step guidance.");
        parts.Add("Request: flight surgeon review of the readings and steps above, with instructions to the crew.");
        if (!mission.LinkUp) parts.Add("Link is down: this packet is held and will be sent when the link returns.");
        return string.Join(" ", parts);
    }

    private async Task<List<PacketFollowUp>> FollowUpAsync(
        Mission mission, Guid crewId, DateTimeOffset now, List<PacketSymptom> symptoms, PacketAlert? alert,
        PacketProtocol? protocol, ProtocolDefinition? def, List<Measurement> recent, Func<DateTimeOffset, string> met, CancellationToken ct)
    {
        var list = new List<PacketFollowUp>();

        // Onset: first trigger symptom in the window, else the flag time.
        if (symptoms.Count > 0)
        {
            var first = symptoms[0];
            list.Add(new("onset", "Onset time?",
                $"First '{first.SymptomCode}' logged at {first.LoggedMet} MET ({Before(now - first.LoggedAt)}).", "symptom log"));
        }
        else if (alert is not null)
            list.Add(new("onset", "Onset time?", $"First flagged at {alert.AtMet} MET ({Before(now - alert.At)}).", "alert"));
        else
            list.Add(new("onset", "Onset time?", "Not recorded.", "not recorded"));

        // Medications: action steps the protocol marks with log: true (e.g. aspirin given or withheld).
        var logged = protocol?.Steps.Where(s => def?.TreeYaml.FirstOrDefault(n => n.Id == s.NodeId)?.Log == true).ToList() ?? [];
        list.Add(logged.Count > 0
            ? new("medications", "Medications taken?", string.Join(" ", logged.Select(s => $"{s.AtMet} MET: {s.Text}")), "protocol run")
            : new("medications", "Medications taken?", "No medication step logged.", "not recorded"));

        list.Add(DailySummary("recent_exercise", "Recent exercise?", "exercise_min", recent, mission, now, "No exercise logged in the last 3 mission days."));
        list.Add(DailySummary("sleep", "Sleep?", "sleep_h", recent, mission, now, "No sleep logged in the last 3 mission days."));

        var env = await db.EnvironmentLogs
            .Where(e => e.MissionId == mission.Id && e.RecordedAt >= now - Window && e.RecordedAt <= now)
            .OrderBy(e => e.RecordedAt)
            .ToListAsync(ct);
        list.Add(env.Count > 0
            ? new("environmental_events", "Environmental events?",
                string.Join("; ", env.Select(e => e.Kind == "solar_particle_event"
                    ? $"solar particle event at {met(e.RecordedAt)} MET"
                    : $"{e.Kind} {e.Value.ToString("0.##", CultureInfo.InvariantCulture)} at {met(e.RecordedAt)} MET")) + ".",
                "environment log")
            : new("environmental_events", "Environmental events?", "None logged in the last 72 h.", "not recorded"));

        return list;
    }

    private static string Before(TimeSpan age) =>
        age < TimeSpan.FromMinutes(1) ? "just before this packet" : $"{MissionClock.FormatAge(age)} before this packet";

    // Latest value per mission day for the last 3 mission days (same day rule as metric_count).
    private PacketFollowUp DailySummary(string key, string question, string metricCode, List<Measurement> recent, Mission mission, DateTimeOffset now, string none)
    {
        var today = ContextEngine.MissionDay(mission.LaunchDate, now);
        var unit = config.Metrics.GetValueOrDefault(metricCode)?.Unit ?? "";
        var days = recent.Where(x => x.MetricCode == metricCode)
            .GroupBy(x => ContextEngine.MissionDay(mission.LaunchDate, x.RecordedAt))
            .Where(g => g.Key > today - 3)
            .OrderBy(g => g.Key)
            .Select(g => $"day {g.Key}: {g.MaxBy(x => x.RecordedAt)!.Value.ToString("0.#", CultureInfo.InvariantCulture)} {unit}")
            .ToList();
        return days.Count > 0
            ? new(key, question, string.Join("; ", days) + ".", "measurements")
            : new(key, question, none, "not recorded");
    }
}
