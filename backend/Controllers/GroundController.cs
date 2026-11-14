using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Auth;
using backend.Configuration;
using backend.Data;
using backend.Entities;
using backend.Responses;
using backend.Services.Engines;
using backend.Services.Missions;

namespace backend.Controllers;

// Ground-side API (/ground UI).
[ApiController]
[Route("api/ground")]
public sealed class GroundController(MissionService missions, ApplicationDbContext db, HealthConfig config) : ControllerBase
{
    // E2 / H3: only what has arrived through the delayed link, newest first, with the age of the data.
    // ?type=Packet,Alert filters by message type (Alert, Packet, Measurement, Symptom, ProtocolStep).
    [HttpGet("inbox")]
    public async Task<ApiResponse<IEnumerable<LinkMessageDto>>> Inbox([FromQuery] string? type, CancellationToken ct)
    {
        Caller.From(Request).RequireGround();
        var types = LinkController.ParseTypes(type);
        var m = await missions.CurrentAsync(ct);
        await missions.DeliverDueAsync(m, ct);
        var now = missions.Now(m);
        var q = db.LinkMessages
            .Where(x => x.MissionId == m.Id && x.Direction == LinkDirection.CrewToGround && x.DeliveredAt != null);
        if (types is not null) q = q.Where(x => types.Contains(x.Type));
        var rows = await q.OrderByDescending(x => x.DeliveredAt).ToListAsync(ct);
        return ApiResponse<IEnumerable<LinkMessageDto>>.Succeeded(rows.Select(x => LinkMessageDto.From(x, now)));
    }

    // D2: crew x metrics grid with the latest status per cell.
    [HttpGet("overview")]
    // Built only from readings already delivered to ground.
    public async Task<ApiResponse<IEnumerable<object>>> Overview(CancellationToken ct)
    {
        Caller.From(Request).RequireGround();
        var m = await missions.CurrentAsync(ct);
        await missions.DeliverDueAsync(m, ct);
        var now = missions.Now(m);
        var crew = await db.CrewMembers.Where(c => c.MissionId == m.Id).OrderBy(c => c.Name).ToListAsync(ct);
        var statusMetrics = config.Metrics.Values.Where(x => x.HasStatus).ToList();
        var result = new List<object>();

        foreach (var c in crew)
        {
            var baselines = await missions.BaselineSnapshotsAsync(c.Id, ct);
            var cells = new List<object>();
            foreach (var metric in statusMetrics)
            {
                var latest = await missions.GroundVisibleMeasurements(now)
                    .Where(x => x.CrewMemberId == c.Id && x.MetricCode == metric.Code
                                && x.Source == HealthMeasurementSource.Inflight && x.RecordedAt <= now)
                    .OrderByDescending(x => x.RecordedAt)
                    .FirstOrDefaultAsync(ct);
                var status = latest is null ? null : StatusEngine.Evaluate(metric, latest.Value, baselines.GetValueOrDefault(metric.Code));
                cells.Add(new { metricCode = metric.Code, value = latest?.Value, recordedAt = latest?.RecordedAt, status });
            }
            result.Add(new { crewMemberId = c.Id, c.Name, c.Role, cells });
        }
        return ApiResponse<IEnumerable<object>>.Succeeded(result);
    }
}
