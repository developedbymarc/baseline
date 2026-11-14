using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests;

// The 3-minute demo script (spec section 13 / 16, steps 2 to 5) as one API-level test,
// driven only through HTTP with the mock-auth headers a real client would send.
public sealed class DemoFlowTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public DemoFlowTests() => _http = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    private Guid _crew;

    private async Task<JsonElement> Call(HttpMethod method, string url, string? role, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (role == "crew") { req.Headers.Add("X-Role", "crew"); req.Headers.Add("X-Crew-Id", _crew.ToString()); }
        if (role == "ground") req.Headers.Add("X-Role", "ground");
        if (body is not null) req.Content = JsonContent.Create(body);
        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"{method} {url} -> {(int)res.StatusCode}: {json}");
        return json.GetProperty("data");
    }

    private Task<JsonElement> Crew(HttpMethod m, string url, object? body = null) => Call(m, url, "crew", body);
    private Task<JsonElement> Ground(string url) => Call(HttpMethod.Get, url, "ground");
    private Task<JsonElement> Presenter(HttpMethod m, string url, object? body = null) => Call(m, url, null, body);

    private async Task<DateTimeOffset> MissionNow() => (await Presenter(HttpMethod.Get, "/api/mission")).GetProperty("missionNow").GetDateTimeOffset();

    [Fact]
    public async Task Demo_script_steps_2_to_5()
    {
        // Step 1 (seed): a 4-person Mars-transit crew, in flight, baselines locked with n.
        var mission = await Presenter(HttpMethod.Get, "/api/mission");
        Assert.Equal("Transit", mission.GetProperty("environment").GetString());
        Assert.Equal("Inflight", mission.GetProperty("mode").GetString());
        Assert.Equal(720, mission.GetProperty("oneWayLatencySeconds").GetDouble());
        var crewList = await Ground("/api/crew");
        Assert.Equal(4, crewList.GetArrayLength());
        _crew = crewList[0].GetProperty("id").GetGuid();

        // ---- Step 2: a seeded reminder fires on the crew tablet ----
        await Presenter(HttpMethod.Post, "/api/demo/clock", new { advanceSeconds = 2 * 3600 }); // 06:05 -> 08:05, BP due at 08:00
        var reminders = await Crew(HttpMethod.Get, $"/api/crew/{_crew}/reminders");
        var bpReminder = reminders.EnumerateArray().Single(r => r.GetProperty("metricCode").GetString() == "bp_sys" && r.GetProperty("due").GetBoolean());
        Assert.True(bpReminder.GetProperty("overdue").GetBoolean());

        // ---- Step 3: a BP reading goes yellow, tied to the personal baseline, with context factors ----
        var bpBaseline = (await Crew(HttpMethod.Get, $"/api/crew/{_crew}/baselines")).EnumerateArray()
            .Single(b => b.GetProperty("metricCode").GetString() == "bp_sys");
        var bpValue = Math.Round(bpBaseline.GetProperty("mean").GetDouble() + 2.1 * bpBaseline.GetProperty("sdEffective").GetDouble(), 1);
        var bp = await Crew(HttpMethod.Post, $"/api/crew/{_crew}/measurements", new { metricCode = "bp_sys", value = bpValue });
        var status = bp.GetProperty("status");
        Assert.Equal("Yellow", status.GetProperty("level").GetString());
        Assert.Equal("zscore", status.GetProperty("rule").GetString());
        Assert.Matches(@"^\+2\.\d SD from your personal baseline \(n=14\)\.$", status.GetProperty("reason").GetString());
        var factors = bp.GetProperty("context").GetProperty("factors").EnumerateArray().Select(f => f.GetProperty("ruleId").GetString()).ToList();
        Assert.Contains("adaptation_window", factors);
        // The reading clears its reminder.
        var openAfter = await Crew(HttpMethod.Get, $"/api/crew/{_crew}/reminders");
        Assert.DoesNotContain(openAfter.EnumerateArray(), r => r.GetProperty("id").GetString() == bpReminder.GetProperty("id").GetString());

        // ---- Step 4: fast-forward 3 weeks (1 mission day per 5 s) ----
        var before = await MissionNow();
        await Presenter(HttpMethod.Post, "/api/demo/clock", new { fastForward = true });
        _factory.Time.Advance(TimeSpan.FromSeconds(21 * 5));
        await Presenter(HttpMethod.Post, "/api/demo/clock", new { fastForward = false });
        Assert.Equal(before.AddDays(21), await MissionNow());
        // (Reports for the drift are checked in the report tests once WP 4.4 exists.)

        // ---- Step 5: chest pain + red reading within 30 mission minutes, latency 12 min ----
        var symptom = await Crew(HttpMethod.Post, $"/api/crew/{_crew}/symptoms", new { symptomCode = "chest_pain", note = "pressure behind the sternum" });
        Assert.Equal("chest_pain", symptom.GetProperty("protocolId").GetString());
        Assert.True((await Presenter(HttpMethod.Get, "/api/mission")).GetProperty("onboardFirst").GetBoolean()); // 12 min > 5 min: on-board first

        _factory.Time.Advance(TimeSpan.FromMinutes(10));
        var red = await Crew(HttpMethod.Post, $"/api/crew/{_crew}/measurements", new { metricCode = "hr_rest", value = 128 });
        Assert.Equal("Red", red.GetProperty("status").GetProperty("level").GetString());
        Assert.Equal("chest_pain", red.GetProperty("protocolId").GetString());
        Assert.Equal("red_reading+symptom", red.GetProperty("protocol").GetProperty("matchedBy").GetString());
        var alertId = red.GetProperty("alertId").GetString();

        // The protocol run is logged with its version.
        var run = await Crew(HttpMethod.Post, $"/api/crew/{_crew}/protocol-runs", new { protocolId = "chest_pain", alertId });
        var runId = run.GetProperty("id").GetString();
        var version = backend.Configuration.HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Protocols["chest_pain"].Version;
        Assert.Equal(version, run.GetProperty("protocolVersion").GetString());
        await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_start", answer = "No" });
        await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_rest" });

        // The packet is queued: deliver_at = now + 12 min, original timestamp kept.
        var queuedAt = await MissionNow();
        var packet = await Crew(HttpMethod.Post, $"/api/crew/{_crew}/packets",
            new { alertId, packet = new { situation = "Chest pain with HR 128" }, note = "Started 10 min before the reading" });
        var packetId = packet.GetProperty("id").GetString();
        var sentAt = packet.GetProperty("sentAt").GetDateTimeOffset();
        Assert.Equal(queuedAt, sentAt);
        Assert.Equal(sentAt.AddMinutes(12), packet.GetProperty("deliverAt").GetDateTimeOffset());
        Assert.Equal("in_flight", packet.GetProperty("state").GetString());

        // Ground cannot see it yet (nor the alert).
        Assert.Empty((await Ground("/api/ground/inbox?type=Packet")).EnumerateArray());
        Assert.DoesNotContain((await Ground($"/api/crew/{_crew}/alerts")).EnumerateArray(), a => a.GetProperty("id").GetString() == alertId);

        // Toggle the link down; a step logged now is held.
        await Presenter(HttpMethod.Post, "/api/demo/link", new { up = false });
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        await Crew(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", new { nodeId = "n_vitals" });
        var heldStep = (await Presenter(HttpMethod.Get, "/api/link/queue?type=ProtocolStep")).EnumerateArray().First();
        Assert.Equal("held", heldStep.GetProperty("state").GetString());
        var heldSentAt = heldStep.GetProperty("sentAt").GetDateTimeOffset();

        // ... and up again: the queue drains with the ORIGINAL timestamps.
        _factory.Time.Advance(TimeSpan.FromMinutes(3));
        var reconnectAt = await MissionNow();
        await Presenter(HttpMethod.Post, "/api/demo/link", new { up = true });
        _factory.Time.Advance(TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(1));

        var inbox = (await Ground("/api/ground/inbox?type=Packet")).EnumerateArray().ToList();
        var delivered = Assert.Single(inbox);
        Assert.Equal(packetId, delivered.GetProperty("id").GetString());
        Assert.Equal(sentAt, delivered.GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal(sentAt.AddMinutes(12), delivered.GetProperty("deliveredAt").GetDateTimeOffset());
        Assert.True(delivered.GetProperty("ageSeconds").GetDouble() >= 20 * 60); // 5 + 3 + 12 min since it was written
        Assert.Equal("Started 10 min before the reading", delivered.GetProperty("payload").GetProperty("note").GetString());

        var steps = (await Ground("/api/ground/inbox?type=ProtocolStep")).EnumerateArray().ToList();
        var drained = steps.Single(s => s.GetProperty("payload").GetProperty("nodeId").ValueKind == JsonValueKind.String
                                         && s.GetProperty("payload").GetProperty("nodeId").GetString() == "n_vitals");
        Assert.Equal(heldSentAt, drained.GetProperty("sentAt").GetDateTimeOffset());
        Assert.Equal(reconnectAt.AddMinutes(12), drained.GetProperty("deliveredAt").GetDateTimeOffset());

        // Ground now sees the alert with its context, and the red reading in the grid.
        Assert.Contains((await Ground($"/api/crew/{_crew}/alerts")).EnumerateArray(), a => a.GetProperty("id").GetString() == alertId);
        var hrCell = (await Ground("/api/ground/overview")).EnumerateArray()
            .Single(r => r.GetProperty("crewMemberId").GetGuid() == _crew).GetProperty("cells").EnumerateArray()
            .Single(c => c.GetProperty("metricCode").GetString() == "hr_rest");
        Assert.Equal("Red", hrCell.GetProperty("status").GetProperty("level").GetString());
    }
}
