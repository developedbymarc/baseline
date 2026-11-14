using backend.Entities;

namespace backend.Tests;

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

public static class TestPaths
{
    // Walks up from the test output folder to the repo's config/ directory.
    public static string ConfigDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "config", "metrics.yaml")))
                dir = dir.Parent;
            return dir is null
                ? throw new DirectoryNotFoundException("config/metrics.yaml not found above the test folder.")
                : Path.Combine(dir.FullName, "config");
        }
    }
}

// Synthetic metric for engine tests, so tests do not break when the content team edits metrics.yaml.
public static class TestMetrics
{
    public static HealthMetric HeartRate(string polarity = "both", double? sdPop = null) => new()
    {
        Code = "hr_test",
        Name = "Test heart rate",
        Unit = "bpm",
        PlausibleMin = 25,
        PlausibleMax = 220,
        NormalLow = 60,
        NormalHigh = 100,
        WarnLow = 50,
        WarnHigh = 100,
        CritLow = 40,
        CritHigh = 120,
        ZWarn = 1.5,
        ZCrit = 2.5,
        Polarity = polarity,
        SdPop = sdPop,
    };

    public static HealthMetric Temperature() => new()
    {
        Code = "temp_test",
        Name = "Test temperature",
        Unit = "°C",
        PlausibleMin = 30,
        PlausibleMax = 43,
        WarnLow = 36.1,
        WarnHigh = 37.5,
        CritLow = 35,
        CritHigh = 38,
        ZWarn = 1.5,
        ZCrit = 2.5,
        Polarity = "both",
    };

    public static HealthMetric Context() => new()
    {
        Code = "mood_test",
        Name = "Test mood",
        Unit = "1-5",
        PlausibleMin = 1,
        PlausibleMax = 5,
        Polarity = "none",
    };
}

// Sets the mock-auth headers from the URL so existing tests act as the obvious caller:
// /api/ground/* -> ground; /api/crew/{id}/* -> that crew member; /api/crew/protocol-runs/* -> the last crew member
// used; /api/crew -> ground. Requests that already carry X-Role are left alone. Role tests use raw clients instead.
public sealed class RoleInferringHandler : DelegatingHandler
{
    private Guid? _lastCrew;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Headers.Contains("X-Role"))
        {
            var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
            if (segments is ["api", "ground", ..] or ["api", "crew"])
                request.Headers.Add("X-Role", "ground");
            else if (segments is ["api", "crew", var id, ..] && Guid.TryParse(id, out var crewId))
            {
                _lastCrew = crewId;
                request.Headers.Add("X-Role", "crew");
                request.Headers.Add("X-Crew-Id", crewId.ToString());
            }
            else if (segments is ["api", "crew", "protocol-runs", ..] && _lastCrew is { } last)
            {
                request.Headers.Add("X-Role", "crew");
                request.Headers.Add("X-Crew-Id", last.ToString());
            }
        }
        return base.SendAsync(request, ct);
    }
}
