using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Entities;
using backend.Responses;
using backend.Services.Missions;

namespace backend.Controllers;

// G3: shows the whole queue (held, in flight, delivered) so the demo can show it draining.
[ApiController]
[Route("api/link")]
public sealed class LinkController(MissionService missions, ApplicationDbContext db) : ControllerBase
{
    // Open (presenter view). ?type=Packet,Alert filters by message type.
    [HttpGet("queue")]
    public async Task<ApiResponse<IEnumerable<LinkMessageDto>>> Queue([FromQuery] string? type, CancellationToken ct)
    {
        var types = ParseTypes(type);
        var m = await missions.CurrentAsync(ct);
        await missions.DeliverDueAsync(m, ct);
        var now = missions.Now(m);
        var q = db.LinkMessages.Where(x => x.MissionId == m.Id && x.SentAt <= now); // not yet "sent" = still in the seeded future
        if (types is not null) q = q.Where(x => types.Contains(x.Type));
        var rows = await q.OrderByDescending(x => x.SentAt).ToListAsync(ct);
        return ApiResponse<IEnumerable<LinkMessageDto>>.Succeeded(rows.Select(x => LinkMessageDto.From(x, now)));
    }

    // "Packet,Alert" -> [Packet, Alert]; null = all types.
    internal static List<LinkMessageType>? ParseTypes(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var result = new List<LinkMessageType>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            result.Add(Enum.TryParse<LinkMessageType>(part, ignoreCase: true, out var t)
                ? t
                : throw new ApiProblem(400, "BAD_TYPE", $"Unknown message type '{part}'. Use {string.Join(", ", Enum.GetNames<LinkMessageType>())}."));
        return result;
    }
}
