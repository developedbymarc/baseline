using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Responses;
using backend.Services.Missions;

namespace backend.Controllers;

// G3: shows the whole queue (held, in flight, delivered) so the demo can show it draining.
[ApiController]
[Route("api/link")]
public sealed class LinkController(MissionService missions, ApplicationDbContext db) : ControllerBase
{
    [HttpGet("queue")]
    public async Task<ApiResponse<IEnumerable<LinkMessageDto>>> Queue(CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.DeliverDueAsync(m, ct);
        var now = missions.Now(m);
        var rows = await db.LinkMessages.Where(x => x.MissionId == m.Id).OrderByDescending(x => x.SentAt).ToListAsync(ct);
        return ApiResponse<IEnumerable<LinkMessageDto>>.Succeeded(rows.Select(x => LinkMessageDto.From(x, now)));
    }
}
