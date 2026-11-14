using Microsoft.AspNetCore.Mvc;
using backend.Auth;
using backend.Entities;
using backend.Responses;
using backend.Services.Missions;
using backend.Services.Reports;

namespace backend.Controllers;

// Biweekly and monthly reports (WP 4.4). Crew read their own member report (all crew-side data);
// ground reads any member report and the whole-crew report, built only from delivered data.
[ApiController]
public sealed class ReportsController(ReportService reports, MissionService missions) : ControllerBase
{
    private static ReportWindow ParseWindow(string window) =>
        window.ToLowerInvariant() switch
        {
            "biweekly" => ReportWindow.Biweekly,
            "monthly" => ReportWindow.Monthly,
            _ => throw new ApiProblem(400, "BAD_WINDOW", "Window must be biweekly or monthly."),
        };

    // Open: lists the fixed periods (no health data).
    [HttpGet("api/reports/periods")]
    public async Task<ApiResponse<IEnumerable<ReportPeriod>>> Periods([FromQuery] string window = "biweekly", CancellationToken ct = default)
    {
        var w = ParseWindow(window);
        var m = await missions.CurrentAsync(ct);
        await reports.StoreCompletedAsync(ct);
        return ApiResponse<IEnumerable<ReportPeriod>>.Succeeded(reports.Periods(m, w, missions.Now(m)));
    }

    [HttpGet("api/crew/{crewId:guid}/reports/{window}/{period}")]
    public async Task<ApiResponse<ReportDto>> Member(Guid crewId, string window, string period, CancellationToken ct)
    {
        var me = Caller.From(Request);
        me.RequireCrewOrGround(crewId);
        var w = ParseWindow(window);
        var m = await missions.CurrentAsync(ct);
        await missions.CrewAsync(m, crewId, ct);
        await reports.StoreCompletedAsync(ct);
        var number = reports.ResolvePeriod(m, w, period, missions.Now(m));
        return ApiResponse<ReportDto>.Succeeded(await reports.BuildAsync(crewId, w, number, groundView: me.IsGround, ct));
    }

    [HttpGet("api/reports/crew/{window}/{period}")]
    public async Task<ApiResponse<ReportDto>> Crew(string window, string period, CancellationToken ct)
    {
        Caller.From(Request).RequireGround();
        var w = ParseWindow(window);
        var m = await missions.CurrentAsync(ct);
        await reports.StoreCompletedAsync(ct);
        var number = reports.ResolvePeriod(m, w, period, missions.Now(m));
        return ApiResponse<ReportDto>.Succeeded(await reports.BuildAsync(null, w, number, groundView: true, ct));
    }
}
