using Microsoft.AspNetCore.Mvc;
using backend.Configuration;
using backend.Entities;
using backend.Responses;
using backend.Services.Missions;

namespace backend.Controllers;

// Serves the versioned YAML content so the frontend never hard-codes limits or protocol text.
[ApiController]
[Route("api/config")]
public sealed class ConfigController(HealthConfig config) : ControllerBase
{
    [HttpGet("metrics")]
    public ApiResponse<IEnumerable<HealthMetric>> Metrics() =>
        ApiResponse<IEnumerable<HealthMetric>>.Succeeded(config.Metrics.Values);

    [HttpGet("protocols")]
    public ApiResponse<IEnumerable<object>> Protocols() =>
        ApiResponse<IEnumerable<object>>.Succeeded(config.Protocols.Values.Select(p => (object)new { p.Id, p.Title, p.Version }));

    [HttpGet("protocols/{id}")]
    public ApiResponse<ProtocolDefinition> Protocol(string id) =>
        config.Protocols.TryGetValue(id, out var p)
            ? ApiResponse<ProtocolDefinition>.Succeeded(p)
            : throw new ApiProblem(404, "NO_PROTOCOL", $"Protocol '{id}' not found.");
}
