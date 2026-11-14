using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests;

public sealed class BaselineApiFactory : WebApplicationFactory<Program>
{
    public ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"baseline-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Health:ConfigDirectory", TestPaths.ConfigDirectory);
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Time);
        });
    }

    // A client whose requests carry the mock-auth headers implied by the URL (see RoleInferringHandler).
    public HttpClient CreateRoleClient() => CreateDefaultClient(new RoleInferringHandler());

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}

// Each test gets a fresh app + database (seeded on start).
public sealed class ApiTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public ApiTests() => _http = _factory.CreateRoleClient();

    // Read from the file so a version bump in chest_pain.yaml does not break these tests.
    private static readonly string ChestPainVersion =
        backend.Configuration.HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Protocols["chest_pain"].Version;
    public void Dispose() => _factory.Dispose();

    private async Task<JsonElement> Get(string url)
    {
        var res = await _http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<(HttpStatusCode, JsonElement)> Post(string url, object body)
    {
        var res = await _http.PostAsJsonAsync(url, body);
        return (res.StatusCode, await res.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<string> FirstCrewId() => (await Get("/api/crew"))[0].GetProperty("id").GetString()!;

    private async Task<double> BaselineMean(string crewId, string metric)
    {
        foreach (var b in (await Get($"/api/crew/{crewId}/baselines")).EnumerateArray())
            if (b.GetProperty("metricCode").GetString() == metric) return b.GetProperty("mean").GetDouble();
        throw new InvalidOperationException("no baseline");
    }

    [Fact]
    public async Task Seeded_mission_is_inflight_with_locked_baselines()
    {
        var mission = await Get("/api/mission");
        Assert.Equal("Inflight", mission.GetProperty("mode").GetString());
        Assert.Equal("Transit", mission.GetProperty("environment").GetString());
        Assert.Equal(720, mission.GetProperty("oneWayLatencySeconds").GetDouble());
        Assert.True(mission.GetProperty("onboardFirst").GetBoolean()); // 12 min > 5 min threshold

        var crew = await Get("/api/crew");
        Assert.Equal(4, crew.GetArrayLength());

        var baselines = await Get($"/api/crew/{crew[0].GetProperty("id").GetString()}/baselines");
        Assert.Equal(6, baselines.GetArrayLength()); // status metrics only; mood / exercise are context inputs
        foreach (var b in baselines.EnumerateArray())
        {
            Assert.Equal(14, b.GetProperty("n").GetInt32());
            Assert.True(b.GetProperty("lockedAt").ValueKind != JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Start_mission_twice_is_rejected()
    {
        var (code, _) = await Post("/api/mission/start", new { });
        Assert.Equal(HttpStatusCode.Conflict, code);
    }

    [Fact]
    public async Task Fahrenheit_entry_needs_confirmation_then_saves_flagged()
    {
        var crew = await FirstCrewId();
        var (code, body) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "temp", value = 98.6 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, code);
        Assert.Contains("Fahrenheit", body.GetProperty("error").GetProperty("message").GetString());

        var (code2, body2) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "temp", value = 98.6, confirmImplausible = true });
        Assert.Equal(HttpStatusCode.OK, code2);
        Assert.Equal("ImplausibleConfirmed", body2.GetProperty("data").GetProperty("measurement").GetProperty("qualityFlag").GetString());
    }

    [Fact]
    public async Task Unknown_metric_and_crew_are_errors()
    {
        var crew = await FirstCrewId();
        var (c1, _) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "glucose", value = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        var (c2, _) = await Post($"/api/crew/{Guid.NewGuid()}/measurements", new { metricCode = "hr_rest", value = 60 });
        Assert.Equal(HttpStatusCode.NotFound, c2);
    }

    [Fact]
    public async Task Value_at_baseline_is_green_and_raises_no_alert()
    {
        var crew = await FirstCrewId();
        var mean = Math.Round(await BaselineMean(crew, "hr_rest"));
        var (code, body) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = mean });
        Assert.Equal(HttpStatusCode.OK, code);
        var data = body.GetProperty("data");
        Assert.Equal("Green", data.GetProperty("status").GetProperty("level").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("alertId").ValueKind);
    }

    [Fact]
    public async Task Red_reading_alert_reaches_ground_only_after_latency_with_original_timestamp()
    {
        var crew = await FirstCrewId();
        var (_, body) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "spo2", value = 85 });
        var status = body.GetProperty("data").GetProperty("status");
        Assert.Equal("Red", status.GetProperty("level").GetString());
        Assert.Contains("threshold", status.GetProperty("rule").GetString());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("data").GetProperty("alertId").ValueKind);

        var queue = await Get("/api/link/queue?type=Alert");
        Assert.Equal("in_flight", queue[0].GetProperty("state").GetString());
        var sentAt = queue[0].GetProperty("sentAt").GetDateTimeOffset();
        Assert.Empty((await Get("/api/ground/inbox")).EnumerateArray());

        _factory.Time.Advance(TimeSpan.FromSeconds(719));
        Assert.Empty((await Get("/api/ground/inbox")).EnumerateArray());

        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        var inbox = await Get("/api/ground/inbox?type=Alert");
        Assert.Equal(1, inbox.GetArrayLength());
        Assert.Equal("Alert", inbox[0].GetProperty("type").GetString());
        Assert.Equal(sentAt, inbox[0].GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal(sentAt.AddSeconds(720), inbox[0].GetProperty("deliveredAt").GetDateTimeOffset());
        Assert.True(inbox[0].GetProperty("ageSeconds").GetDouble() >= 720);
        Assert.Equal("spo2", inbox[0].GetProperty("payload").GetProperty("metricCode").GetString());
    }

    [Fact]
    public async Task Link_down_holds_packet_and_reconnect_drains_queue()
    {
        var crew = await FirstCrewId();
        await Post("/api/demo/link", new { up = false });

        var (code, body) = await Post($"/api/crew/{crew}/packets",
            new { packet = new { situation = "chest pain", background = "synthetic" }, note = "felt dizzy" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal("held", body.GetProperty("data").GetProperty("state").GetString());
        var sentAt = body.GetProperty("data").GetProperty("sentAt").GetDateTimeOffset();

        _factory.Time.Advance(TimeSpan.FromHours(1));
        Assert.Empty((await Get("/api/ground/inbox")).EnumerateArray());

        var (_, state) = await Post("/api/demo/link", new { up = true, oneWayLatencySeconds = 60 });
        Assert.True(state.GetProperty("data").GetProperty("linkUp").GetBoolean());
        Assert.Equal("in_flight", (await Get("/api/link/queue"))[0].GetProperty("state").GetString());

        _factory.Time.Advance(TimeSpan.FromSeconds(61));
        var inbox = await Get("/api/ground/inbox?type=Packet");
        Assert.Equal("Packet", inbox[0].GetProperty("type").GetString());
        Assert.Equal(sentAt, inbox[0].GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal("felt dizzy", inbox[0].GetProperty("payload").GetProperty("note").GetString());
        Assert.Equal("chest pain", inbox[0].GetProperty("payload").GetProperty("packet").GetProperty("situation").GetString());
    }

    [Fact]
    public async Task Crew_side_keeps_working_with_link_down()
    {
        var crew = await FirstCrewId();
        await Post("/api/demo/link", new { up = false });
        var (code, _) = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 130 });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.NotEmpty((await Get($"/api/crew/{crew}/alerts")).EnumerateArray());
        Assert.NotEmpty((await Get($"/api/crew/{crew}/reminders")).EnumerateArray());
        Assert.Equal("held", (await Get("/api/link/queue"))[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task Fast_forward_moves_mission_clock_and_reminders_follow()
    {
        var crew = await FirstCrewId();
        var before = (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset();

        var (_, s) = await Post("/api/demo/clock", new { fastForward = true });
        Assert.Equal(86400.0 / 5, s.GetProperty("data").GetProperty("clockRate").GetDouble());
        _factory.Time.Advance(TimeSpan.FromSeconds(10)); // 2 mission days
        var after = (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset();
        Assert.Equal(before.AddDays(2), after);

        // Daily reminders: earlier days are missed, today's is overdue / due.
        var all = await Get($"/api/crew/{crew}/reminders?openOnly=false");
        Assert.Contains(all.EnumerateArray(), r => r.GetProperty("status").GetString() == "Missed");
        var open = await Get($"/api/crew/{crew}/reminders");
        Assert.Contains(open.EnumerateArray(), r => r.GetProperty("overdue").GetBoolean());
    }

    [Fact]
    public async Task Measurement_completes_its_reminder()
    {
        var crew = await FirstCrewId();
        // Seeded launch is 06:00 UTC and the clock starts at 06:05; hr_rest is due daily at 08:00.
        await Post("/api/demo/clock", new { advanceSeconds = 2 * 3600 });
        var mean = Math.Round(await BaselineMean(crew, "hr_rest"));
        await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = mean });

        var all = await Get($"/api/crew/{crew}/reminders?openOnly=false");
        Assert.Contains(all.EnumerateArray(), r =>
            r.GetProperty("metricCode").GetString() == "hr_rest" && r.GetProperty("status").GetString() == "Done");
    }

    [Fact]
    public async Task Chest_pain_symptom_opens_protocol_and_run_logs_steps()
    {
        var crew = await FirstCrewId();
        var (_, sym) = await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        var data = sym.GetProperty("data");
        Assert.Equal("chest_pain", data.GetProperty("protocolId").GetString());
        var alertId = data.GetProperty("alertId").GetString();

        var protocol = await Get("/api/config/protocols/chest_pain");
        Assert.Equal("n_start", protocol.GetProperty("start").GetString());

        var (_, run) = await Post($"/api/crew/{crew}/protocol-runs", new { protocolId = "chest_pain", alertId });
        var runId = run.GetProperty("data").GetProperty("id").GetString();
        Assert.Equal(ChestPainVersion, run.GetProperty("data").GetProperty("protocolVersion").GetString());

        var (okCode, _) = await Post($"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_start", answer = "No" });
        Assert.Equal(HttpStatusCode.OK, okCode);
        var (badCode, _) = await Post($"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_fake" });
        Assert.Equal(HttpStatusCode.BadRequest, badCode);

        var (badOutcome, err) = await Post($"/api/crew/protocol-runs/{runId}/end", new { outcome = "made_up" });
        Assert.Equal(HttpStatusCode.BadRequest, badOutcome);
        Assert.Equal("BAD_OUTCOME", err.GetProperty("error").GetProperty("code").GetString());
        var (noOutcome, _) = await Post($"/api/crew/protocol-runs/{runId}/end", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noOutcome);

        var (_, ended) = await Post($"/api/crew/protocol-runs/{runId}/end", new { outcome = "hold_and_wait_for_ground" });
        var steps = ended.GetProperty("data").GetProperty("steps");
        Assert.Equal(2, steps.GetArrayLength());
        Assert.Equal("n_start", steps[0].GetProperty("nodeId").GetString());

        var (again, _) = await Post($"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_rest" });
        Assert.Equal(HttpStatusCode.Conflict, again);
    }

    [Fact]
    public async Task Symptom_not_in_any_trigger_raises_no_alert()
    {
        var crew = await FirstCrewId();
        var (code, sym) = await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "headache" });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(JsonValueKind.Null, sym.GetProperty("data").GetProperty("protocolId").ValueKind);
        Assert.Equal(JsonValueKind.Null, sym.GetProperty("data").GetProperty("alertId").ValueKind);
        Assert.Empty((await Get($"/api/crew/{crew}/alerts")).EnumerateArray());
    }

    [Fact]
    public async Task Protocol_version_is_stored_on_the_run()
    {
        var crew = Guid.Parse(await FirstCrewId());
        var (_, run) = await Post($"/api/crew/{crew}/protocol-runs", new { protocolId = "chest_pain" });
        var runId = Guid.Parse(run.GetProperty("data").GetProperty("id").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<backend.Data.ApplicationDbContext>();
        Assert.Equal(ChestPainVersion, db.ProtocolRuns.Single(r => r.Id == runId).ProtocolVersion);
    }

    [Fact]
    public async Task Baselines_warn_when_small_sample_has_no_sd_pop()
    {
        var crew = Guid.Parse(await FirstCrewId());
        // Seeded baselines have n=14: no warning.
        foreach (var b in (await Get($"/api/crew/{crew}/baselines")).EnumerateArray())
            Assert.Equal(JsonValueKind.Null, b.GetProperty("warning").ValueKind);

        // A fourth-crew-style member with only 5 readings: hr_rest (sd_pop null) warns, bp_sys (sd_pop set) shrinks.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<backend.Data.ApplicationDbContext>();
            var missionId = db.Missions.Single().Id;
            var c = new backend.Entities.CrewMember { MissionId = missionId, Name = "New (synthetic)", Role = "r", Sex = "F", Age = 40, HeightCm = 170 };
            db.CrewMembers.Add(c);
            db.Baselines.Add(new backend.Entities.Baseline { CrewMemberId = c.Id, MetricCode = "hr_rest", Mean = 60, StandardDeviation = 3, SampleSize = 5, LockedAt = DateTimeOffset.UtcNow });
            db.Baselines.Add(new backend.Entities.Baseline { CrewMemberId = c.Id, MetricCode = "bp_sys", Mean = 110, StandardDeviation = 5, SampleSize = 5, LockedAt = DateTimeOffset.UtcNow });
            db.SaveChanges();
            crew = c.Id;
        }

        var rows = (await Get($"/api/crew/{crew}/baselines")).EnumerateArray().ToDictionary(b => b.GetProperty("metricCode").GetString()!);
        Assert.Contains("sd_pop", rows["hr_rest"].GetProperty("warning").GetString());
        Assert.False(rows["hr_rest"].GetProperty("shrunk").GetBoolean());
        Assert.Equal(3, rows["hr_rest"].GetProperty("sdEffective").GetDouble());
        Assert.Equal(JsonValueKind.Null, rows["bp_sys"].GetProperty("warning").ValueKind);
        Assert.True(rows["bp_sys"].GetProperty("shrunk").GetBoolean());
        Assert.True(rows["bp_sys"].GetProperty("sdEffective").GetDouble() > 5);
    }

    [Fact]
    public async Task Ground_overview_is_crew_by_metric_grid()
    {
        var crew = await FirstCrewId();
        await Post($"/api/crew/{crew}/measurements", new { metricCode = "spo2", value = 85 });
        _factory.Time.Advance(TimeSpan.FromSeconds(721)); // ground sees it only after the link delay
        var grid = await Get("/api/ground/overview");
        Assert.Equal(4, grid.GetArrayLength());
        var row = grid.EnumerateArray().Single(r => r.GetProperty("crewMemberId").GetString() == crew);
        Assert.Equal(6, row.GetProperty("cells").GetArrayLength());
        var spo2 = row.GetProperty("cells").EnumerateArray().Single(c => c.GetProperty("metricCode").GetString() == "spo2");
        Assert.Equal("Red", spo2.GetProperty("status").GetProperty("level").GetString());
    }

    [Fact]
    public async Task Metrics_config_is_served_from_yaml()
    {
        var metrics = await Get("/api/config/metrics");
        Assert.Contains(metrics.EnumerateArray(), m => m.GetProperty("code").GetString() == "bp_sys");
    }

    [Fact]
    public async Task Clock_rejects_bad_input()
    {
        var (c1, _) = await Post("/api/demo/clock", new { rate = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        var (c2, _) = await Post("/api/demo/clock", new { advanceSeconds = -5 });
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        var (c3, _) = await Post("/api/demo/link", new { oneWayLatencySeconds = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, c3);
    }

    [Fact]
    public async Task Reseed_restores_a_clean_mission()
    {
        var crew = await FirstCrewId();
        await Post($"/api/crew/{crew}/measurements", new { metricCode = "spo2", value = 85 });
        var (code, _) = await Post("/api/demo/reseed", new { });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty((await Get("/api/link/queue")).EnumerateArray());
        Assert.Equal(4, (await Get("/api/crew")).GetArrayLength());
    }
}
