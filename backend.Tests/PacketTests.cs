using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;
using backend.Entities;

namespace backend.Tests;

// SBAR handoff packet (WP 4.5, H1-H3): built from stored data, one template, readable as one object.
public sealed class PacketTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public PacketTests() => _http = _factory.CreateRoleClient();
    public void Dispose() => _factory.Dispose();

    private async Task<(HttpStatusCode Code, JsonElement Body)> Send(HttpMethod m, string url, object? body = null)
    {
        var req = new HttpRequestMessage(m, url) { Content = body is null ? null : JsonContent.Create(body) };
        var res = await _http.SendAsync(req);
        return (res.StatusCode, await res.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<JsonElement> Ok(HttpMethod m, string url, object? body = null)
    {
        var (code, json) = await Send(m, url, body);
        Assert.True(code == HttpStatusCode.OK, $"{m} {url} -> {(int)code}: {json}");
        return json.GetProperty("data");
    }

    private async Task<string> CrewId(int i = 0) => (await Ok(HttpMethod.Get, "/api/crew"))[i].GetProperty("id").GetString()!;
    private async Task<DateTimeOffset> Now() => (await Ok(HttpMethod.Get, "/api/mission")).GetProperty("missionNow").GetDateTimeOffset();

    private void AddRows(Action<ApplicationDbContext> add)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        add(db);
        db.SaveChanges();
    }

    private static List<string> Codes(JsonElement arr, string prop = "metricCode") =>
        arr.EnumerateArray().Select(x => x.GetProperty(prop).GetString()!).ToList();

    private static JsonElement FollowUp(JsonElement data, string key) =>
        data.GetProperty("followUp").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key);

    [Fact]
    public async Task Red_bp_builds_a_draft_with_readings_baselines_context_and_follow_up()
    {
        var crew = await CrewId();
        await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 74 });
        await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "temp", value = 36.8 });
        _factory.Time.Advance(TimeSpan.FromMinutes(30));
        var r = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "bp_sys", value = 145 });
        Assert.Equal("Red", r.GetProperty("status").GetProperty("level").GetString());

        var p = r.GetProperty("packetDraft");
        Assert.Equal("baseline.sbar.v1", p.GetProperty("schema").GetString());
        Assert.Equal(r.GetProperty("alertId").GetString(), p.GetProperty("alertId").GetString());
        var d = p.GetProperty("data");

        // Situation: the triggering reading.
        var trigger = d.GetProperty("trigger");
        Assert.Equal("red_reading", trigger.GetProperty("kind").GetString());
        Assert.Equal("bp_sys", trigger.GetProperty("alert").GetProperty("metricCode").GetString());
        Assert.Equal(145, trigger.GetProperty("alert").GetProperty("value").GetDouble());

        // Last 72 h of RELEVANT readings: bp_sys plus the chest pain protocol's metrics; not temp.
        var readings = Codes(d.GetProperty("measurements72h"));
        Assert.Contains("bp_sys", readings);
        Assert.Contains("hr_rest", readings);
        Assert.DoesNotContain("temp", readings);
        Assert.Contains(d.GetProperty("measurements72h").EnumerateArray(), m => m.GetProperty("metricCode").GetString() == "bp_sys"
            && m.GetProperty("value").GetDouble() == 145 && m.GetProperty("level").GetString() == "Red");

        // Baselines with z and n.
        var bpBase = d.GetProperty("baselines").EnumerateArray().Single(b => b.GetProperty("metricCode").GetString() == "bp_sys");
        Assert.Equal(14, bpBase.GetProperty("n").GetInt32());
        Assert.True(bpBase.GetProperty("latestZ").GetDouble() > 2.5);
        Assert.Contains("n=14", bpBase.GetProperty("summary").GetString());
        Assert.Equal(4, d.GetProperty("baselines").GetArrayLength());

        // Mission context and context factors from the alert.
        Assert.Equal("Transit", d.GetProperty("mission").GetProperty("environment").GetString());
        Assert.Equal(720, d.GetProperty("mission").GetProperty("oneWayLatencySeconds").GetDouble());
        Assert.Equal("12 min", d.GetProperty("mission").GetProperty("oneWayLatencyText").GetString());
        Assert.True(d.GetProperty("mission").GetProperty("inAdaptationWindow").GetBoolean());
        Assert.Contains("adaptation_window", Codes(d.GetProperty("contextFactors"), "ruleId"));
        Assert.Equal("Red", d.GetProperty("contextOutcome").GetProperty("level").GetString());

        // Pre-answered follow-up checklist, in order, filled from stored data where possible.
        Assert.Equal(["onset", "medications", "recent_exercise", "sleep", "environmental_events"], Codes(d.GetProperty("followUp"), "key"));
        Assert.Equal("alert", FollowUp(d, "onset").GetProperty("filledFrom").GetString());
        Assert.Equal("not recorded", FollowUp(d, "medications").GetProperty("filledFrom").GetString());
        Assert.Equal("measurements", FollowUp(d, "sleep").GetProperty("filledFrom").GetString()); // pre-flight nights
        Assert.Equal("not recorded", FollowUp(d, "environmental_events").GetProperty("filledFrom").GetString());

        // One text, all four SBAR sections.
        var text = p.GetProperty("text").GetString()!;
        foreach (var section in new[] { "SITUATION", "BACKGROUND", "ASSESSMENT", "RECOMMENDATION" })
            Assert.Contains("\n" + section + "\n", text);
        Assert.Contains("Systolic blood pressure 145 mmHg", text);
        Assert.Contains("n=14", text);
        Assert.Contains("SYNTHETIC", text);
        Assert.Null(p.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Baseline_list_covers_exactly_the_relevant_metrics()
    {
        var crew = await CrewId();
        var r = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "bp_sys", value = 145 });
        var codes = Codes(r.GetProperty("packetDraft").GetProperty("data").GetProperty("baselines")).Order().ToList();
        Assert.Equal(["bp_dia", "bp_sys", "hr_rest", "spo2"], codes);
    }

    [Fact]
    public async Task Red_temperature_packet_is_limited_to_temperature()
    {
        var crew = await CrewId();
        var r = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "temp", value = 39.2 });
        var d = r.GetProperty("packetDraft").GetProperty("data");
        Assert.Equal(["temp"], Codes(d.GetProperty("baselines")));
    }

    [Fact]
    public async Task Queued_packet_is_one_object_ground_reads_with_no_other_call()
    {
        var crew = await CrewId();
        var r = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "bp_sys", value = 145 });
        var alertId = r.GetProperty("alertId").GetString();

        var msg = await Ok(HttpMethod.Post, $"/api/crew/{crew}/packets", new { alertId, note = "  Headache since breakfast  " });
        Assert.Equal("Packet", msg.GetProperty("type").GetString());
        Assert.Equal(alertId, msg.GetProperty("alertId").GetString());
        var payload = msg.GetProperty("payload");
        Assert.Equal(msg.GetProperty("sentAt").GetDateTimeOffset(), payload.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("Headache since breakfast", payload.GetProperty("note").GetString());

        _factory.Time.Advance(TimeSpan.FromSeconds(721));
        var delivered = (await Ok(HttpMethod.Get, "/api/ground/inbox?type=Packet")).EnumerateArray().Single();
        Assert.StartsWith("Written 12 min ago", delivered.GetProperty("ageOfData").GetString());
        var p = delivered.GetProperty("payload");
        var d = p.GetProperty("data");
        foreach (var key in new[] { "mission", "crew", "trigger", "measurements72h", "baselines", "contextFactors", "followUp", "recommendation" })
            Assert.True(d.TryGetProperty(key, out _), $"missing data.{key}");
        Assert.Equal(await CrewId(), d.GetProperty("crew").GetProperty("id").GetString());
        Assert.Contains("Crew note: \"Headache since breakfast\"", p.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Chest_pain_run_packet_has_steps_symptoms_medications_and_environment()
    {
        var crew = await CrewId();
        var now = await Now();
        AddRows(db =>
        {
            var missionId = db.Missions.Single().Id;
            db.EnvironmentLogs.Add(new EnvironmentLog { MissionId = missionId, Kind = "co2_mmhg", Value = 3.4, RecordedAt = now.AddHours(-2) });
            db.EnvironmentLogs.Add(new EnvironmentLog { MissionId = missionId, Kind = "solar_particle_event", Value = 1, RecordedAt = now.AddHours(-1) });
        });
        await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "exercise_min", value = 20 });

        await Ok(HttpMethod.Post, $"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain", note = "pressure behind the sternum" });
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        var red = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 128 });
        var alertId = red.GetProperty("alertId").GetString();

        // Starting the protocol builds a draft at once (H1).
        var run = await Ok(HttpMethod.Post, $"/api/crew/{crew}/protocol-runs", new { protocolId = "chest_pain", alertId });
        var runId = run.GetProperty("id").GetString();
        var draft = run.GetProperty("packetDraft");
        Assert.Equal(runId, draft.GetProperty("runId").GetString());
        Assert.Empty(draft.GetProperty("data").GetProperty("protocol").GetProperty("steps").EnumerateArray());

        foreach (var (node, answer) in new (string, string?)[]
                 { ("n_start", "No"), ("n_rest", null), ("n_vitals", null), ("n_duration", "Yes"), ("n_other", "No"), ("n_aspirin_q", "No"), ("n_aspirin", null) })
        {
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
            await Ok(HttpMethod.Post, $"/api/crew/protocol-runs/{runId}/steps", new { nodeId = node, answer });
        }

        // Queue by run: alert taken from the run; rebuilt with every step so far.
        var msg = await Ok(HttpMethod.Post, $"/api/crew/{crew}/packets", new { runId });
        Assert.Equal(alertId, msg.GetProperty("alertId").GetString());
        var p = msg.GetProperty("payload");
        var d = p.GetProperty("data");

        var steps = d.GetProperty("protocol").GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["n_start", "n_rest", "n_vitals", "n_duration", "n_other", "n_aspirin_q", "n_aspirin"], steps.Select(s => s.GetProperty("nodeId").GetString()));
        Assert.Equal("Yes", steps[3].GetProperty("answer").GetString());
        Assert.StartsWith("Has the chest discomfort lasted", steps[3].GetProperty("text").GetString());
        Assert.All(steps, s => Assert.Matches(@"^0d \d\d:\d\d:\d\d$", s.GetProperty("atMet").GetString()));
        var version = backend.Configuration.HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Protocols["chest_pain"].Version;
        Assert.Equal(version, d.GetProperty("protocol").GetProperty("version").GetString());

        var symptom = d.GetProperty("trigger").GetProperty("symptoms").EnumerateArray().Single();
        Assert.Equal("chest_pain", symptom.GetProperty("symptomCode").GetString());
        Assert.Equal("pressure behind the sternum", symptom.GetProperty("note").GetString());

        Assert.Equal("symptom log", FollowUp(d, "onset").GetProperty("filledFrom").GetString());
        Assert.Contains("'chest_pain'", FollowUp(d, "onset").GetProperty("answer").GetString());
        var meds = FollowUp(d, "medications");
        Assert.Equal("protocol run", meds.GetProperty("filledFrom").GetString());
        Assert.Contains("aspirin", meds.GetProperty("answer").GetString());
        Assert.Contains("exercise", FollowUp(d, "recent_exercise").GetProperty("question").GetString()!.ToLowerInvariant());
        Assert.Contains("20 min", FollowUp(d, "recent_exercise").GetProperty("answer").GetString());
        var env = FollowUp(d, "environmental_events").GetProperty("answer").GetString()!;
        Assert.Contains("co2_mmhg 3.4", env);
        Assert.Contains("solar particle event", env);

        Assert.Contains("in progress", d.GetProperty("recommendation").GetString());
        var text = p.GetProperty("text").GetString()!;
        Assert.Contains($"Protocol: Chest pain / suspected cardiac event v{version}", text);
        Assert.Contains("-> Yes", text);
        Assert.Contains("Symptom: chest_pain", text);
    }

    [Fact]
    public async Task Downgraded_and_unexplained_flags_read_correctly_in_the_text()
    {
        var crew = await CrewId();
        var now = await Now();
        // Two short nights on two mission days, then a z-yellow heart rate: downgraded by short_sleep.
        AddRows(db =>
        {
            foreach (var h in new[] { 20, 44 })
                db.Measurements.Add(new Measurement { CrewMemberId = Guid.Parse(crew), MetricCode = "sleep_h", Value = 4.5, RecordedAt = now.AddHours(-h), Source = HealthMeasurementSource.Inflight });
        });
        var hrBase = (await Ok(HttpMethod.Get, $"/api/crew/{crew}/baselines")).EnumerateArray().Single(b => b.GetProperty("metricCode").GetString() == "hr_rest");
        var y = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements",
            new { metricCode = "hr_rest", value = hrBase.GetProperty("mean").GetDouble() + 2 * hrBase.GetProperty("sdEffective").GetDouble() });
        Assert.Equal(JsonValueKind.Null, y.GetProperty("packetDraft").ValueKind); // drafts are automatic only for red
        var draft = await Ok(HttpMethod.Get, $"/api/crew/{crew}/packets/draft?alertId={y.GetProperty("alertId").GetString()}");
        Assert.Contains("Flag lowered from Yellow to Green by short_sleep", draft.GetProperty("text").GetString());

        // Day 22: no adaptation window, no other factor -> "unexplained deviation".
        var b = await CrewId(1);
        await Ok(HttpMethod.Post, "/api/demo/clock", new { advanceSeconds = 21 * 86400 });
        var red = await Ok(HttpMethod.Post, $"/api/crew/{b}/measurements", new { metricCode = "spo2", value = 85 });
        Assert.Contains("No known context factor matched (unexplained deviation).", red.GetProperty("packetDraft").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Draft_is_deterministic_for_the_same_data_and_time()
    {
        var crew = await CrewId();
        var r = await Ok(HttpMethod.Post, $"/api/crew/{crew}/measurements", new { metricCode = "bp_sys", value = 145 });
        var url = $"/api/crew/{crew}/packets/draft?alertId={r.GetProperty("alertId").GetString()}";
        var a = await Ok(HttpMethod.Get, url);
        var b = await Ok(HttpMethod.Get, url);
        Assert.Equal(a.GetProperty("text").GetString(), b.GetProperty("text").GetString());
        Assert.Equal(a.GetProperty("data").ToString(), b.GetProperty("data").ToString());
    }

    [Fact]
    public async Task Invalid_packet_requests()
    {
        var a = await CrewId(0);
        var b = await CrewId(1);
        var (c1, e1) = await Send(HttpMethod.Post, $"/api/crew/{a}/packets", new { note = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, c1);
        Assert.Equal("NO_TRIGGER", e1.GetProperty("error").GetProperty("code").GetString());

        var r = await Ok(HttpMethod.Post, $"/api/crew/{a}/measurements", new { metricCode = "bp_sys", value = 145 });
        var (c2, e2) = await Send(HttpMethod.Post, $"/api/crew/{a}/packets", new { alertId = r.GetProperty("alertId").GetString(), note = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Equal("NOTE_TOO_LONG", e2.GetProperty("error").GetProperty("code").GetString());

        // Another crew member's run or alert cannot be packed.
        var runB = await Ok(HttpMethod.Post, $"/api/crew/{b}/protocol-runs", new { protocolId = "chest_pain" });
        var (c3, e3) = await Send(HttpMethod.Post, $"/api/crew/{a}/packets", new { runId = runB.GetProperty("id").GetString() });
        Assert.Equal(HttpStatusCode.NotFound, c3);
        Assert.Equal("NO_RUN", e3.GetProperty("error").GetProperty("code").GetString());
    }
}
