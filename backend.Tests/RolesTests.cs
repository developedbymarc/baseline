using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests;

// Mock auth: X-Role crew|ground, X-Crew-Id for crew. Crew act on their own data only; ground reads only
// what the link has delivered; demo, config, mission and link-queue endpoints stay open for the presenter.
public sealed class RolesTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _raw;

    public RolesTests() => _raw = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    private async Task<(HttpStatusCode Code, JsonElement Body)> Send(HttpMethod method, string url, string? role, Guid? crewId = null, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (role is not null) req.Headers.Add("X-Role", role);
        if (crewId is not null) req.Headers.Add("X-Crew-Id", crewId.ToString());
        if (body is not null) req.Content = JsonContent.Create(body);
        var res = await _raw.SendAsync(req);
        return (res.StatusCode, await res.Content.ReadFromJsonAsync<JsonElement>());
    }

    private Task<(HttpStatusCode, JsonElement)> Crew(HttpMethod m, string url, Guid me, object? body = null) => Send(m, url, "crew", me, body);
    private Task<(HttpStatusCode, JsonElement)> Ground(HttpMethod m, string url, object? body = null) => Send(m, url, "ground", null, body);

    private async Task<(Guid A, Guid B)> TwoCrew()
    {
        var (_, body) = await Ground(HttpMethod.Get, "/api/crew");
        var data = body.GetProperty("data");
        return (data[0].GetProperty("id").GetGuid(), data[1].GetProperty("id").GetGuid());
    }

    private static string Code(JsonElement body) => body.GetProperty("error").GetProperty("code").GetString()!;

    // ---------- Headers ----------

    [Fact]
    public async Task Missing_or_bad_role_is_401()
    {
        var (a, _) = await TwoCrew();
        var (c1, b1) = await Send(HttpMethod.Get, $"/api/crew/{a}/baselines", null);
        Assert.Equal(HttpStatusCode.Unauthorized, c1);
        Assert.Equal("NO_ROLE", Code(b1));

        var (c2, _) = await Send(HttpMethod.Get, $"/api/crew/{a}/baselines", "admin");
        Assert.Equal(HttpStatusCode.Unauthorized, c2);

        var (c3, b3) = await Send(HttpMethod.Get, $"/api/crew/{a}/baselines", "crew");
        Assert.Equal(HttpStatusCode.Unauthorized, c3);
        Assert.Equal("NO_CREW_ID", Code(b3));
    }

    [Fact]
    public async Task Role_header_is_case_insensitive()
    {
        var (a, _) = await TwoCrew();
        var (code, _) = await Send(HttpMethod.Get, $"/api/crew/{a}/baselines", "Ground");
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Theory]
    [InlineData("/api/mission")]
    [InlineData("/api/config/metrics")]
    [InlineData("/api/config/protocols/chest_pain")]
    [InlineData("/api/link/queue")]
    public async Task Presenter_and_config_reads_are_open(string url)
    {
        var (code, _) = await Send(HttpMethod.Get, url, null);
        Assert.Equal(HttpStatusCode.OK, code);
    }

    [Fact]
    public async Task Demo_controls_are_open()
    {
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/api/demo/clock", null, body: new { advanceSeconds = 60 })).Code);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/api/demo/link", null, body: new { up = false })).Code);
    }

    // ---------- Crew: own data only ----------

    [Fact]
    public async Task Crew_list_shows_only_self_and_ground_sees_all()
    {
        var (a, _) = await TwoCrew();
        var (_, mine) = await Crew(HttpMethod.Get, "/api/crew", a);
        Assert.Equal([a], mine.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()));
        var (_, all) = await Ground(HttpMethod.Get, "/api/crew");
        Assert.Equal(4, all.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Crew_can_use_their_own_endpoints()
    {
        var (a, _) = await TwoCrew();
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Get, $"/api/crew/{a}/baselines", a)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Get, $"/api/crew/{a}/measurements", a)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Get, $"/api/crew/{a}/reminders", a)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Get, $"/api/crew/{a}/alerts", a)).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Post, $"/api/crew/{a}/measurements", a, new { metricCode = "hr_rest", value = 70 })).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Post, $"/api/crew/{a}/symptoms", a, new { symptomCode = "headache" })).Item1);
    }

    public static TheoryData<string, string> CrewOnOtherCrew => new()
    {
        { "GET", "baselines" },
        { "GET", "measurements" },
        { "GET", "reminders" },
        { "GET", "alerts" },
        { "POST", "measurements" },
        { "POST", "symptoms" },
        { "POST", "packets" },
        { "POST", "protocol-runs" },
    };

    [Theory]
    [MemberData(nameof(CrewOnOtherCrew))]
    public async Task Crew_cannot_touch_another_crew_members_data(string method, string resource)
    {
        var (a, b) = await TwoCrew();
        object? body = (method, resource) switch
        {
            ("POST", "measurements") => new { metricCode = "hr_rest", value = 70 },
            ("POST", "symptoms") => new { symptomCode = "chest_pain" },
            ("POST", "packets") => new { note = "x" },
            ("POST", "protocol-runs") => new { protocolId = "chest_pain" },
            _ => null,
        };
        var (code, res) = await Crew(new HttpMethod(method), $"/api/crew/{b}/{resource}", a, body);
        Assert.Equal(HttpStatusCode.Forbidden, code);
        Assert.Equal("FORBIDDEN", Code(res));
    }

    [Fact]
    public async Task Crew_cannot_log_steps_on_another_crew_members_run()
    {
        var (a, b) = await TwoCrew();
        var (_, run) = await Crew(HttpMethod.Post, $"/api/crew/{b}/protocol-runs", b, new { protocolId = "chest_pain" });
        var runId = run.GetProperty("data").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", a, new { nodeId = "n_start" })).Item1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/end", a, new { outcome = "emergency_cpr" })).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", b, new { nodeId = "n_start" })).Item1);
    }

    [Theory]
    [InlineData("/api/ground/inbox")]
    [InlineData("/api/ground/overview")]
    public async Task Crew_cannot_use_ground_endpoints(string url)
    {
        var (a, _) = await TwoCrew();
        Assert.Equal(HttpStatusCode.Forbidden, (await Crew(HttpMethod.Get, url, a)).Item1);
    }

    // ---------- Ground: read-only, delivered data only ----------

    [Fact]
    public async Task Ground_cannot_write_crew_data_or_read_reminders()
    {
        var (a, _) = await TwoCrew();
        Assert.Equal(HttpStatusCode.Forbidden, (await Ground(HttpMethod.Post, $"/api/crew/{a}/measurements", new { metricCode = "hr_rest", value = 70 })).Item1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Ground(HttpMethod.Post, $"/api/crew/{a}/symptoms", new { symptomCode = "chest_pain" })).Item1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Ground(HttpMethod.Post, $"/api/crew/{a}/protocol-runs", new { protocolId = "chest_pain" })).Item1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Ground(HttpMethod.Get, $"/api/crew/{a}/reminders")).Item1);
        Assert.Equal(HttpStatusCode.OK, (await Ground(HttpMethod.Get, $"/api/crew/{a}/baselines")).Item1);
    }

    [Fact]
    public async Task Ground_sees_preflight_data_at_once()
    {
        var (a, _) = await TwoCrew();
        var (_, body) = await Ground(HttpMethod.Get, $"/api/crew/{a}/measurements?metric=hr_rest");
        Assert.Equal(14, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Ground_cannot_read_undelivered_reading_alert_or_cell()
    {
        var (a, _) = await TwoCrew();
        var (_, posted) = await Crew(HttpMethod.Post, $"/api/crew/{a}/measurements", a, new { metricCode = "spo2", value = 85 });
        var measurementId = posted.GetProperty("data").GetProperty("measurement").GetProperty("id").GetGuid();
        var recordedAt = posted.GetProperty("data").GetProperty("measurement").GetProperty("recordedAt").GetDateTimeOffset();

        async Task<bool> GroundHasReading() =>
            (await Ground(HttpMethod.Get, $"/api/crew/{a}/measurements?metric=spo2")).Item2.GetProperty("data")
                .EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == measurementId);
        async Task<int> GroundAlerts() => (await Ground(HttpMethod.Get, $"/api/crew/{a}/alerts")).Item2.GetProperty("data").GetArrayLength();
        async Task<JsonElement> Spo2Cell() =>
            (await Ground(HttpMethod.Get, "/api/ground/overview")).Item2.GetProperty("data").EnumerateArray()
                .Single(r => r.GetProperty("crewMemberId").GetGuid() == a).GetProperty("cells").EnumerateArray()
                .Single(c => c.GetProperty("metricCode").GetString() == "spo2");

        // In transit: crew sees everything, ground sees nothing.
        Assert.Equal(1, (await Crew(HttpMethod.Get, $"/api/crew/{a}/alerts", a)).Item2.GetProperty("data").GetArrayLength());
        Assert.False(await GroundHasReading());
        Assert.Equal(0, await GroundAlerts());
        Assert.Equal(JsonValueKind.Null, (await Spo2Cell()).GetProperty("value").ValueKind);
        Assert.Empty((await Ground(HttpMethod.Get, "/api/ground/inbox")).Item2.GetProperty("data").EnumerateArray());

        _factory.Time.Advance(TimeSpan.FromSeconds(719));
        Assert.False(await GroundHasReading());
        Assert.Equal(0, await GroundAlerts());

        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(await GroundHasReading());
        Assert.Equal(1, await GroundAlerts());
        Assert.Equal(85, (await Spo2Cell()).GetProperty("value").GetDouble());
        var row = (await Ground(HttpMethod.Get, $"/api/crew/{a}/measurements?metric=spo2")).Item2.GetProperty("data")
            .EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == measurementId);
        Assert.Equal(recordedAt.AddSeconds(720), row.GetProperty("syncedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Held_messages_stay_invisible_to_ground_until_reconnect_plus_latency()
    {
        var (a, _) = await TwoCrew();
        await Send(HttpMethod.Post, "/api/demo/link", null, body: new { up = false });
        await Crew(HttpMethod.Post, $"/api/crew/{a}/measurements", a, new { metricCode = "hr_rest", value = 130 });
        _factory.Time.Advance(TimeSpan.FromHours(6));
        Assert.Equal(0, (await Ground(HttpMethod.Get, $"/api/crew/{a}/alerts")).Item2.GetProperty("data").GetArrayLength());

        await Send(HttpMethod.Post, "/api/demo/link", null, body: new { up = true });
        _factory.Time.Advance(TimeSpan.FromSeconds(719));
        Assert.Equal(0, (await Ground(HttpMethod.Get, $"/api/crew/{a}/alerts")).Item2.GetProperty("data").GetArrayLength());
        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, (await Ground(HttpMethod.Get, $"/api/crew/{a}/alerts")).Item2.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Symptoms_and_protocol_steps_reach_ground_only_through_the_link()
    {
        var (a, _) = await TwoCrew();
        await Crew(HttpMethod.Post, $"/api/crew/{a}/symptoms", a, new { symptomCode = "chest_pain", note = "pressure" });
        var (_, run) = await Crew(HttpMethod.Post, $"/api/crew/{a}/protocol-runs", a, new { protocolId = "chest_pain" });
        var runId = run.GetProperty("data").GetProperty("id").GetString();
        await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", a, new { nodeId = "n_start", answer = "No" });

        var (_, before) = await Ground(HttpMethod.Get, "/api/ground/inbox?type=Symptom,ProtocolStep");
        Assert.Empty(before.GetProperty("data").EnumerateArray());

        _factory.Time.Advance(TimeSpan.FromSeconds(721));
        var (_, after) = await Ground(HttpMethod.Get, "/api/ground/inbox?type=Symptom,ProtocolStep");
        var msgs = after.GetProperty("data").EnumerateArray().ToList();
        Assert.Contains(msgs, m => m.GetProperty("type").GetString() == "Symptom"
                                   && m.GetProperty("payload").GetProperty("symptomCode").GetString() == "chest_pain");
        var steps = msgs.Where(m => m.GetProperty("type").GetString() == "ProtocolStep").ToList();
        Assert.Equal(new[] { "started", "step" }.Order(), steps.Select(s => s.GetProperty("payload").GetProperty("event").GetString()!).Order());
        var version = backend.Configuration.HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Protocols["chest_pain"].Version;
        Assert.All(steps, s => Assert.Equal(version, s.GetProperty("payload").GetProperty("protocolVersion").GetString()));
    }

    [Fact]
    public async Task Unknown_message_type_filter_is_400()
    {
        var (code, body) = await Ground(HttpMethod.Get, "/api/ground/inbox?type=Telegram");
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Equal("BAD_TYPE", Code(body));
    }
}
