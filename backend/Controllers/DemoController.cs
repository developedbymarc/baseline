using Microsoft.AspNetCore.Mvc;
using backend.Data;
using backend.Responses;
using backend.Services.Clock;
using backend.Services.Missions;
using backend.Services.Seed;

namespace backend.Controllers;

public sealed record ClockRequest(double? Rate, bool? Paused, double? AdvanceSeconds, bool? FastForward);
public sealed record LinkRequest(bool? Up, double? OneWayLatencySeconds);

// Presenter controls (G2, G3). Not role-protected: auth is mocked for the demo.
[ApiController]
[Route("api/demo")]
public sealed class DemoController(
    MissionService missions, MissionClockService clock, ApplicationDbContext db, DemoSeeder seeder, IConfiguration configuration)
    : ControllerBase
{
    private double OnboardFirstThreshold => configuration.GetValue("Link:OnboardFirstThresholdSeconds", 300.0);

    [HttpPost("clock")]
    public async Task<ApiResponse<MissionStateDto>> Clock(ClockRequest req, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        var real = clock.RealNow;
        if (req.FastForward is { } ff) MissionClock.SetRate(m, ff ? MissionClock.DemoFastForwardRate : 1.0, real);
        if (req.Rate is { } rate) MissionClock.SetRate(m, rate, real);
        if (req.Paused is { } paused) MissionClock.SetPaused(m, paused, real);
        if (req.AdvanceSeconds is { } s) MissionClock.Advance(m, TimeSpan.FromSeconds(s), real);
        await db.SaveChangesAsync(ct);
        await missions.DeliverDueAsync(m, ct);
        return ApiResponse<MissionStateDto>.Succeeded(MissionStateDto.From(m, missions.Now(m), OnboardFirstThreshold));
    }

    [HttpPost("link")]
    public async Task<ApiResponse<MissionStateDto>> Link(LinkRequest req, CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        await missions.SetLinkAsync(m, req.Up, req.OneWayLatencySeconds, ct);
        await missions.DeliverDueAsync(m, ct);
        return ApiResponse<MissionStateDto>.Succeeded(MissionStateDto.From(m, missions.Now(m), OnboardFirstThreshold));
    }

    // Wipes the SQLite file and reloads the synthetic demo mission.
    [HttpPost("reseed")]
    public async Task<ApiResponse> Reseed(CancellationToken ct)
    {
        await seeder.ResetAndSeedAsync(ct: ct);
        return ApiResponse.Succeeded("Demo mission reloaded (synthetic data).");
    }
}
