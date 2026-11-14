using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;
using backend.Entities;

namespace backend.Tests;

public sealed class ContextAndTriggerApiTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public ContextAndTriggerApiTests() => _http = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    private async Task<JsonElement> Get(string url)
    {
        var res = await _http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<JsonElement> Post(string url, object body)
    {
        var res = await _http.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<string> CrewId() => (await Get("/api/crew"))[0].GetProperty("id").GetString()!;

    private async Task<DateTimeOffset> MissionNow() => (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset();

    private void AddRows(Action<ApplicationDbContext> add)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        add(db);
        db.SaveChanges();
    }

    // A heart-rate value that is yellow by z-score only (z = +2.0, under the 100 bpm limit).
    private async Task<double> ZYellowHeartRate(string crew)
    {
        foreach (var b in (await Get($"/api/crew/{crew}/baselines")).EnumerateArray())
            if (b.GetProperty("metricCode").GetString() == "hr_rest")
                return b.GetProperty("mean").GetDouble() + 2 * b.GetProperty("sdEffective").GetDouble();
        throw new InvalidOperationException();
    }

    private static string? Str(JsonElement e, string p) => e.GetProperty(p).ValueKind == JsonValueKind.Null ? null : e.GetProperty(p).GetString();

    // ---------- Context factors ----------

    [Fact]
    public async Task Yellow_reading_on_day_1_shows_adaptation_only_and_is_not_downgraded()
    {
        var crew = await CrewId();
        var hr = await ZYellowHeartRate(crew);
        var d = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = hr });

        Assert.Equal("Yellow", d.GetProperty("status").GetProperty("level").GetString());
        Assert.Equal("high", d.GetProperty("status").GetProperty("direction").GetString());
        var ctx = d.GetProperty("context");
        Assert.Equal(["adaptation_window"], ctx.GetProperty("factors").EnumerateArray().Select(f => f.GetProperty("ruleId").GetString()));
        Assert.False(ctx.GetProperty("downgraded").GetBoolean());
        Assert.Equal("Yellow", ctx.GetProperty("level").GetString());
    }

    [Fact]
    public async Task Short_sleep_downgrades_yellow_and_the_alert_still_logs_the_original_flag()
    {
        var crew = await CrewId();
        var now = await MissionNow();
        AddRows(db =>
        {
            foreach (var hoursAgo in new[] { 20, 44 })
                db.Measurements.Add(new Measurement
                {
                    CrewMemberId = Guid.Parse(crew), MetricCode = "sleep_h", Value = 4.5,
                    RecordedAt = now.AddHours(-hoursAgo), Source = HealthMeasurementSource.InFlight,
                });
        });

        var hr = await ZYellowHeartRate(crew);
        var d = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = hr });
        var ctx = d.GetProperty("context");
        Assert.Equal("Yellow", ctx.GetProperty("originalLevel").GetString());
        Assert.Equal("Green", ctx.GetProperty("level").GetString());
        Assert.True(ctx.GetProperty("downgraded").GetBoolean());
        Assert.Equal(["short_sleep"], ctx.GetProperty("downgradedBy").EnumerateArray().Select(x => x.GetString()));
        var factor = ctx.GetProperty("factors").EnumerateArray().Single(f => f.GetProperty("ruleId").GetString() == "short_sleep");
        Assert.Equal(["C1", "C2"], factor.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()));

        // I4: never hidden. The alert exists with the original level and the context stored on it.
        var alert = (await Get($"/api/crew/{crew}/alerts"))[0];
        Assert.Equal("Yellow", alert.GetProperty("level").GetString());
        Assert.True(alert.GetProperty("context").GetProperty("downgraded").GetBoolean());

        // Ground receives the factors too.
        _factory.Time.Advance(TimeSpan.FromSeconds(721));
        var inbox = (await Get("/api/ground/inbox"))[0];
        Assert.True(inbox.GetProperty("payload").GetProperty("context").GetProperty("downgraded").GetBoolean());
    }

    [Fact]
    public async Task Environment_log_entries_feed_context_rules()
    {
        var crew = await CrewId();
        var now = await MissionNow();
        AddRows(db =>
        {
            var missionId = db.Missions.Single().Id;
            db.EnvironmentLogs.Add(new EnvironmentLog { MissionId = missionId, Kind = "kp", Value = 6, RecordedAt = now.AddHours(-2) });
            db.EnvironmentLogs.Add(new EnvironmentLog { MissionId = missionId, Kind = "solar_particle_event", Value = 1, RecordedAt = now.AddHours(-1) });
        });
        var d = await Post($"/api/crew/{crew}/measurements", new { metricCode = "spo2", value = 85 });
        var ids = d.GetProperty("context").GetProperty("factors").EnumerateArray().Select(f => f.GetProperty("ruleId").GetString()).ToList();
        Assert.Empty(ids); // space weather rules do not apply to spo2

        var hr = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 130 });
        var hrIds = hr.GetProperty("context").GetProperty("factors").EnumerateArray().Select(f => f.GetProperty("ruleId").GetString()).ToList();
        Assert.Contains("space_weather_kp", hrIds);
        Assert.Contains("space_weather_spe", hrIds);
        Assert.Equal("Red", hr.GetProperty("context").GetProperty("level").GetString());
    }

    [Fact]
    public async Task Green_reading_has_no_context()
    {
        var crew = await CrewId();
        var b = (await Get($"/api/crew/{crew}/baselines")).EnumerateArray().Single(x => x.GetProperty("metricCode").GetString() == "hr_rest");
        var d = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = Math.Round(b.GetProperty("mean").GetDouble()) });
        Assert.Equal(JsonValueKind.Null, d.GetProperty("context").ValueKind);
    }

    // ---------- Chest pain trigger window (window_min 30, mission time, either order) ----------

    private const string RedHeartRate = "130"; // above crit_high 120

    private async Task<JsonElement> RedReading(string crew) =>
        await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = double.Parse(RedHeartRate) });

    [Fact]
    public async Task Red_reading_without_symptom_does_not_open_protocol()
    {
        var crew = await CrewId();
        var d = await RedReading(crew);
        Assert.Equal("Red", d.GetProperty("status").GetProperty("level").GetString());
        Assert.Null(Str(d, "protocolId"));
    }

    [Fact]
    public async Task Red_reading_on_metric_not_in_red_metrics_does_not_open_protocol()
    {
        var crew = await CrewId();
        await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        var d = await Post($"/api/crew/{crew}/measurements", new { metricCode = "temp", value = 39.0 });
        Assert.Equal("Red", d.GetProperty("status").GetProperty("level").GetString());
        Assert.Null(Str(d, "protocolId"));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(30 * 60, true)]        // exactly 30 min: inside
    [InlineData(30 * 60 + 1, false)]   // 30 min 1 s: outside
    public async Task Symptom_then_red_reading(int secondsBetween, bool opens)
    {
        var crew = await CrewId();
        var sym = await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        var symptomId = sym.GetProperty("symptomLogId").GetString();
        _factory.Time.Advance(TimeSpan.FromSeconds(secondsBetween));

        var d = await RedReading(crew);
        if (opens)
        {
            Assert.Equal("chest_pain", Str(d, "protocolId"));
            Assert.Equal("red_reading+symptom", d.GetProperty("protocol").GetProperty("matchedBy").GetString());
            Assert.Equal([symptomId], d.GetProperty("protocol").GetProperty("matchedSymptomLogIds").EnumerateArray().Select(x => x.GetString()));
        }
        else
        {
            Assert.Null(Str(d, "protocolId"));
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(30 * 60, true)]
    [InlineData(30 * 60 + 1, false)]
    public async Task Red_reading_then_symptom(int secondsBetween, bool matches)
    {
        var crew = await CrewId();
        var reading = await RedReading(crew);
        Assert.Null(Str(reading, "protocolId"));
        var alertId = reading.GetProperty("alertId").GetString();
        _factory.Time.Advance(TimeSpan.FromSeconds(secondsBetween));

        var sym = await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        // The symptom always offers its protocol (story C6); the window decides whether the red reading is matched.
        Assert.Equal("chest_pain", Str(sym, "protocolId"));
        var p = sym.GetProperty("protocol");
        var matched = p.GetProperty("matchedRedAlertIds").EnumerateArray().Select(x => x.GetString()).ToList();
        if (matches)
        {
            Assert.Equal("symptom+red_reading", p.GetProperty("matchedBy").GetString());
            Assert.Equal([alertId], matched);
        }
        else
        {
            Assert.Equal("symptom", p.GetProperty("matchedBy").GetString());
            Assert.Empty(matched);
        }
    }

    [Fact]
    public async Task Window_uses_mission_time_under_fast_forward()
    {
        var crew = await CrewId();
        await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        await Post("/api/demo/clock", new { fastForward = true });
        _factory.Time.Advance(TimeSpan.FromSeconds(1)); // 1 real second = 4.8 mission hours
        var d = await RedReading(crew);
        Assert.Null(Str(d, "protocolId"));
    }

    [Fact]
    public async Task Other_crew_members_symptom_does_not_count()
    {
        var crewList = await Get("/api/crew");
        var a = crewList[0].GetProperty("id").GetString()!;
        var b = crewList[1].GetProperty("id").GetString()!;
        await Post($"/api/crew/{b}/symptoms", new { symptomCode = "chest_pain" });
        var d = await RedReading(a);
        Assert.Null(Str(d, "protocolId"));
    }
}
