using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Responses;
using backend.Services.Engines;
using backend.Services.Link;
using backend.Services.Missions;
using backend.Services.Reminders;

namespace backend.Controllers;

public sealed record SubmitMeasurementRequest(string MetricCode, double Value, bool ConfirmImplausible = false, string? Note = null);
public sealed record QueuePacketRequest(Guid? AlertId, JsonElement Packet, string? Note);
public sealed record LogSymptomRequest(string SymptomCode, string? Note);
public sealed record StartProtocolRequest(string ProtocolId, Guid? AlertId);
public sealed record ProtocolStepRequest(string NodeId, string? Answer);
public sealed record EndProtocolRequest(string? Outcome);

public sealed record CrewMemberDto(Guid Id, string Name, string Role, int Age, string Sex, int HeightCm, string? Notes)
{
    public static CrewMemberDto From(CrewMember c) => new(c.Id, c.Name, c.Role, c.Age, c.Sex, c.HeightCm, c.Notes);
}

public sealed record AlertDto(Guid Id, Guid CrewMemberId, Guid? MeasurementId, StatusLevel Level, string Reason, string RuleId, DateTimeOffset CreatedAt, string? AcknowledgedBy, JsonElement? Context)
{
    public static AlertDto From(Alert a) => new(a.Id, a.CrewMemberId, a.MeasurementId, a.Level, a.Reason, a.RuleId, a.CreatedAt, a.AcknowledgedBy,
        a.ContextJson is null ? null : JsonDocument.Parse(a.ContextJson).RootElement.Clone());
}

public sealed record ProtocolRunDto(Guid Id, Guid CrewMemberId, string ProtocolId, string ProtocolVersion, Guid? AlertId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, JsonElement Steps)
{
    public static ProtocolRunDto From(ProtocolRun r) => new(r.Id, r.CrewMemberId, r.ProtocolId, r.ProtocolVersion, r.AlertId, r.StartedAt, r.EndedAt,
        JsonDocument.Parse(r.StepsJson).RootElement.Clone());
}

// Crew-side API (/crew UI). Everything here works with the link down: nothing waits on ground.
[ApiController]
[Route("api/crew")]
public sealed class CrewController(MissionService missions, ApplicationDbContext db, HealthConfig config) : ControllerBase
{
    [HttpGet]
    public async Task<ApiResponse<IEnumerable<CrewMemberDto>>> List(CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        var crew = await db.CrewMembers.Where(c => c.MissionId == m.Id).OrderBy(c => c.Name).ToListAsync(ct);
        return ApiResponse<IEnumerable<CrewMemberDto>>.Succeeded(crew.Select(CrewMemberDto.From));
    }

    [HttpGet("{crewId:guid}/baselines")]
    public async Task<ApiResponse<IEnumerable<object>>> Baselines(Guid crewId, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.CrewAsync(m, crewId, ct);
        var rows = await db.Baselines.Where(b => b.CrewMemberId == crewId).ToListAsync(ct);
        return ApiResponse<IEnumerable<object>>.Succeeded(rows.Select(b =>
        {
            var sdPop = config.Metrics.GetValueOrDefault(b.MetricCode)?.SdPop;
            var fallback = b.SampleSize < BaselineEngine.ShrinkBelowN && sdPop is null;
            return (object)new
            {
                b.MetricCode,
                b.Mean,
                Sd = b.StandardDeviation,
                SdEffective = BaselineEngine.EffectiveSd(b.StandardDeviation, b.SampleSize, sdPop),
                Shrunk = BaselineEngine.IsShrunk(b.SampleSize, sdPop),
                N = b.SampleSize,
                b.LockedAt,
                Warning = fallback
                    ? $"Only {b.SampleSize} pre-flight readings and no population SD (sd_pop) in metrics.yaml, so the personal SD is used without shrinkage."
                    : null,
            };
        }));
    }

    // D1: history for charts. days = 7 / 14 / 30, omit for the full mission (pre-flight included).
    [HttpGet("{crewId:guid}/measurements")]
    public async Task<ApiResponse<IEnumerable<object>>> Measurements(Guid crewId, [FromQuery] string? metric, [FromQuery] int? days, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.CrewAsync(m, crewId, ct);
        var now = missions.Now(m);
        var q = db.Measurements.Where(x => x.CrewMemberId == crewId && x.RecordedAt <= now);
        if (metric is not null) q = q.Where(x => x.MetricCode == metric);
        if (days is { } d) { var from = now.AddDays(-d); q = q.Where(x => x.RecordedAt >= from); }
        var rows = await q.OrderBy(x => x.RecordedAt).ToListAsync(ct);
        var baselines = await missions.BaselineSnapshotsAsync(crewId, ct);

        return ApiResponse<IEnumerable<object>>.Succeeded(rows.Select(x =>
        {
            var met = config.Metrics.GetValueOrDefault(x.MetricCode);
            var status = met is null || x.Source == HealthMeasurementSource.Preflight
                ? null
                : StatusEngine.Evaluate(met, x.Value, baselines.GetValueOrDefault(x.MetricCode));
            return (object)new { x.Id, x.MetricCode, x.Value, x.RecordedAt, x.Source, x.QualityFlag, x.Note, Status = status };
        }));
    }

    // C2/C3: returns 422 with the plausibility message until the crew member confirms or retakes.
    [HttpPost("{crewId:guid}/measurements")]
    public async Task<IActionResult> Submit(Guid crewId, SubmitMeasurementRequest req, CancellationToken ct)
    {
        var r = await missions.SubmitMeasurementAsync(crewId, req.MetricCode, req.Value, req.ConfirmImplausible, req.Note, ct);
        if (r.NeedsConfirmation)
            return UnprocessableEntity(ApiResponse.Failed(r.Plausibility.Message!, "IMPLAUSIBLE_VALUE"));
        return Ok(ApiResponse<object>.Succeeded(new
        {
            measurement = new { r.Measurement!.Id, r.Measurement.MetricCode, r.Measurement.Value, r.Measurement.RecordedAt, r.Measurement.QualityFlag },
            status = r.Status,
            // Context factors and the policy outcome (I1-I4). The flag in status is the original one.
            context = r.Context,
            alertId = r.AlertId,
            protocolId = r.Protocol?.ProtocolId,
            protocol = r.Protocol,
        }));
    }

    // B2/B3: due and overdue reminders from the crew-side schedule.
    [HttpGet("{crewId:guid}/reminders")]
    public async Task<ApiResponse<IEnumerable<object>>> Reminders(Guid crewId, [FromQuery] bool openOnly = true, CancellationToken ct = default)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.CrewAsync(m, crewId, ct);
        await missions.SyncRemindersAsync(m, crewId, ct);
        var now = missions.Now(m);
        var q = db.ReminderInstances.Include(i => i.Rule).Where(i => i.Rule.CrewMemberId == crewId && i.DueAt <= now.AddDays(1));
        if (openOnly) q = q.Where(i => i.Status == ReminderStatus.Pending);
        var rows = await q.OrderBy(i => i.DueAt).ToListAsync(ct);
        return ApiResponse<IEnumerable<object>>.Succeeded(rows.Select(i => (object)new
        {
            i.Id,
            i.Rule.MetricCode,
            i.DueAt,
            DueMet = (i.DueAt - m.LaunchDate).TotalSeconds,
            i.Status,
            Due = i.DueAt <= now,
            Overdue = ReminderRules.IsOverdue(i, now),
            i.CompletedAt,
        }));
    }

    [HttpGet("{crewId:guid}/alerts")]
    public async Task<ApiResponse<IEnumerable<AlertDto>>> Alerts(Guid crewId, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.CrewAsync(m, crewId, ct);
        var rows = await db.Alerts.Where(a => a.CrewMemberId == crewId).OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
        return ApiResponse<IEnumerable<AlertDto>>.Succeeded(rows.Select(AlertDto.From));
    }

    // C6 / demo step 5: a symptom listed in a protocol's trigger.symptoms raises a red alert and offers that protocol.
    [HttpPost("{crewId:guid}/symptoms")]
    public async Task<ApiResponse<object>> LogSymptom(Guid crewId, LogSymptomRequest req, CancellationToken ct)
    {
        var r = await missions.LogSymptomAsync(crewId, req.SymptomCode, req.Note, ct);
        return ApiResponse<object>.Succeeded(new
        {
            symptomLogId = r.SymptomLogId,
            alertId = r.AlertId,
            protocolId = r.Protocol?.ProtocolId,
            protocol = r.Protocol,
        });
    }

    // H1/H2: the packet JSON comes from the packet builder (WP 4.5); stored as a LinkMessage payload.
    [HttpPost("{crewId:guid}/packets")]
    public async Task<ApiResponse<LinkMessageDto>> QueuePacket(Guid crewId, QueuePacketRequest req, CancellationToken ct)
    {
        var msg = await missions.QueuePacketAsync(crewId, req.AlertId, req.Packet, req.Note, ct);
        var m = await missions.CurrentAsync(ct);
        return ApiResponse<LinkMessageDto>.Succeeded(LinkMessageDto.From(msg, missions.Now(m)));
    }

    // F3: protocol runs log every step with its mission time.
    [HttpPost("{crewId:guid}/protocol-runs")]
    public async Task<ApiResponse<ProtocolRunDto>> StartProtocol(Guid crewId, StartProtocolRequest req, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        var crew = await missions.CrewAsync(m, crewId, ct);
        if (!config.Protocols.TryGetValue(req.ProtocolId, out var protocol))
            throw new ApiProblem(404, "NO_PROTOCOL", $"Protocol '{req.ProtocolId}' not found.");
        var run = new ProtocolRun
        {
            CrewMemberId = crew.Id,
            ProtocolId = protocol.Id,
            ProtocolVersion = protocol.Version,
            AlertId = req.AlertId,
            StartedAt = missions.Now(m),
        };
        db.ProtocolRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return ApiResponse<ProtocolRunDto>.Succeeded(ProtocolRunDto.From(run));
    }

    [HttpPost("protocol-runs/{runId:guid}/steps")]
    public async Task<ApiResponse<ProtocolRunDto>> LogStep(Guid runId, ProtocolStepRequest req, CancellationToken ct)
    {
        var (m, run) = await OpenRunAsync(runId, ct);
        var protocol = config.Protocols[run.ProtocolId];
        if (protocol.TreeYaml.All(n => n.Id != req.NodeId))
            throw new ApiProblem(400, "NO_NODE", $"Node '{req.NodeId}' is not in protocol '{run.ProtocolId}'.");
        var steps = JsonNode.Parse(run.StepsJson)!.AsArray();
        steps.Add(new JsonObject { ["nodeId"] = req.NodeId, ["answer"] = req.Answer, ["at"] = missions.Now(m) });
        run.StepsJson = steps.ToJsonString();
        await db.SaveChangesAsync(ct);
        return ApiResponse<ProtocolRunDto>.Succeeded(ProtocolRunDto.From(run));
    }

    [HttpPost("protocol-runs/{runId:guid}/end")]
    public async Task<ApiResponse<ProtocolRunDto>> EndProtocol(Guid runId, EndProtocolRequest req, CancellationToken ct)
    {
        var (m, run) = await OpenRunAsync(runId, ct);
        var valid = HealthConfig.Outcomes(config.Protocols[run.ProtocolId]);
        if (req.Outcome is null || !valid.Contains(req.Outcome))
            throw new ApiProblem(400, "BAD_OUTCOME",
                $"Outcome must be one of the protocol's end outcomes: {string.Join(", ", valid.Order())}.");
        var now = missions.Now(m);
        var steps = JsonNode.Parse(run.StepsJson)!.AsArray();
        steps.Add(new JsonObject { ["nodeId"] = "end", ["answer"] = req.Outcome, ["at"] = now });
        run.StepsJson = steps.ToJsonString();
        run.EndedAt = now;
        await db.SaveChangesAsync(ct);
        return ApiResponse<ProtocolRunDto>.Succeeded(ProtocolRunDto.From(run));
    }

    private async Task<(Mission, ProtocolRun)> OpenRunAsync(Guid runId, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        var run = await db.ProtocolRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
                  ?? throw new ApiProblem(404, "NO_RUN", $"Protocol run {runId} not found.");
        if (run.EndedAt is not null) throw new ApiProblem(409, "RUN_ENDED", "This protocol run has already ended.");
        return (m, run);
    }
}
