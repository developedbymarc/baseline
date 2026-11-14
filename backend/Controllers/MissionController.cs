using Microsoft.AspNetCore.Mvc;
using backend.Responses;
using backend.Services.Missions;

namespace backend.Controllers;

[ApiController]
[Route("api/mission")]
public sealed class MissionController(MissionService missions, IConfiguration configuration) : ControllerBase
{
    private double OnboardFirstThreshold => configuration.GetValue("Link:OnboardFirstThresholdSeconds", 300.0);

    [HttpGet]
    public async Task<ApiResponse<MissionStateDto>> Get(CancellationToken ct)
    {
        var m = await missions.CurrentAsync(ct);
        return ApiResponse<MissionStateDto>.Succeeded(MissionStateDto.From(m, missions.Now(m), OnboardFirstThreshold));
    }

    // A4: compute and lock baselines, switch to in-flight. The seed already does this; kept for completeness.
    [HttpPost("start")]
    public async Task<ApiResponse<object>> Start(CancellationToken ct)
    {
        var created = await missions.StartMissionAsync(ct);
        return ApiResponse<object>.Succeeded(new { baselinesLocked = created.Count });
    }
}
