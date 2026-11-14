using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;
using backend.Entities;
using backend.Services.Reports;

namespace backend.Tests;

public sealed class ReportApiTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;
    private readonly Xunit.Abstractions.ITestOutputHelper _out;

    public ReportApiTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _out = output;
        _http = _factory.CreateRoleClient();
    }
    public void Dispose() => _factory.Dispose();

    private async Task<(HttpStatusCode Code, JsonElement Body)> Send(HttpMethod m, string url, object? body = null, string? role = null, Guid? crew = null)
    {
        var req = new HttpRequestMessage(m, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (role is not null) req.Headers.Add("X-Role", role);
        if (crew is not null) req.Headers.Add("X-Crew-Id", crew.ToString());
        var res = await _http.SendAsync(req);
        return (res.StatusCode, await res.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<JsonElement> Ok(HttpMethod m, string url, object? body = null, string? role = null, Guid? crew = null)
    {
        var (code, json) = await Send(m, url, body, role, crew);
        Assert.True(code == HttpStatusCode.OK, $"{m} {url} -> {(int)code}: {json}");
        return json.GetProperty("data");
    }

    private async Task<Guid> CrewId(int i = 0) => (await Ok(HttpMethod.Get, "/api/crew")).EnumerateArray().ElementAt(i).GetProperty("id").GetGuid();

    private T WithDb<T>(Func<ApplicationDbContext, T> f)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = f(db);
        db.SaveChanges();
        return r;
    }

    private DateTimeOffset Launch => WithDb(db => db.Missions.Single().LaunchDate);

    // In-flight reading on mission day `day` at 08:30 UTC (launch is 06:00), already delivered to ground.
    private void Reading(Guid crew, string metric, int day, double value) => WithDb(db =>
    {
        var at = db.Missions.Single().LaunchDate.AddDays(day - 1).AddHours(2.5);
        db.Measurements.Add(new Measurement { CrewMemberId = crew, MetricCode = metric, Value = value, RecordedAt = at, Source = HealthMeasurementSource.Inflight, SyncedAt = at });
        return 0;
    });

    private double BaselineMean(Guid crew, string metric) => WithDb(db => db.Baselines.Single(b => b.CrewMemberId == crew && b.MetricCode == metric).Mean);

    private async Task AdvanceDays(double days) => await Ok(HttpMethod.Post, "/api/demo/clock", new { advanceSeconds = days * 86400 });

    private static JsonElement Row(JsonElement report, string metric, int member = 0) =>
        report.GetProperty("members")[member].GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("metricCode").GetString() == metric);

    [Fact]
    public async Task Fixed_periods_from_launch_and_completed_ones_are_stored()
    {
        var bi = await Ok(HttpMethod.Get, "/api/reports/periods?window=biweekly");
        var p1 = Assert.Single(bi.EnumerateArray());
        Assert.Equal(1, p1.GetProperty("startDay").GetInt32());
        Assert.Equal(14, p1.GetProperty("endDay").GetInt32());
        Assert.False(p1.GetProperty("complete").GetBoolean());
        Assert.Equal(Launch, p1.GetProperty("start").GetDateTimeOffset());

        await AdvanceDays(14); // now day 15
        bi = await Ok(HttpMethod.Get, "/api/reports/periods?window=biweekly");
        Assert.Equal(2, bi.GetArrayLength());
        Assert.True(bi[0].GetProperty("complete").GetBoolean());
        Assert.Equal(Launch.AddDays(14), bi[1].GetProperty("start").GetDateTimeOffset());
        var monthly = await Ok(HttpMethod.Get, "/api/reports/periods?window=monthly");
        Assert.Equal(30, monthly[0].GetProperty("endDay").GetInt32());

        // Biweekly period 1 is stored once: 4 member reports + 1 crew report (crew_id empty).
        var stored = WithDb(db => db.Reports.Where(r => r.Window == ReportWindow.Biweekly).Select(r => r.CrewMemberId).ToList());
        Assert.Equal(5, stored.Count);
        Assert.Single(stored, id => id == null);
        await Ok(HttpMethod.Get, "/api/reports/periods?window=biweekly");
        Assert.Equal(5, WithDb(db => db.Reports.Count(r => r.Window == ReportWindow.Biweekly)));
    }

    [Fact]
    public async Task Planted_bp_drift_shows_as_rising_with_z_and_highlight()
    {
        var a = await CrewId();
        var mean = BaselineMean(a, "bp_sys");
        for (var day = 15; day <= 28; day++)
        {
            Reading(a, "bp_sys", day, Math.Round(mean + (day - 15) * 1.0));
            Reading(a, "hr_rest", day, Math.Round(BaselineMean(a, "hr_rest")));
        }
        // Day 29, 09:05: period 2 is complete and its last daily slot (day 28 08:00) closed at day 29 08:00.
        await AdvanceDays(28 + 3.0 / 24);

        var report = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/2", role: "crew", crew: a);
        Assert.Equal("Member", report.GetProperty("scope").GetString());
        Assert.Equal("crew", report.GetProperty("view").GetString());
        Assert.True(report.GetProperty("complete").GetBoolean());
        Assert.False(report.GetProperty("adaptationWindow").GetProperty("overlaps").GetBoolean());

        var bp = Row(report, "bp_sys");
        Assert.Equal(14, bp.GetProperty("windowN").GetInt32());
        Assert.Equal("rising", bp.GetProperty("trend").GetString());
        Assert.Equal(1.0, bp.GetProperty("trendSlopePerDay").GetDouble(), 1);
        Assert.Equal(14, bp.GetProperty("baselineN").GetInt32());
        Assert.True(bp.GetProperty("zSd").GetDouble() > 1);
        Assert.True(bp.GetProperty("zShift").GetDouble() > bp.GetProperty("zSd").GetDouble());
        Assert.Equal(0, bp.GetProperty("missed").GetInt32());
        Assert.False(bp.GetProperty("lowConfidence").GetBoolean());

        var hr = Row(report, "hr_rest");
        Assert.Equal("stable", hr.GetProperty("trend").GetString());

        // No temperature readings at all: 14 missed and low confidence.
        var temp = Row(report, "temp");
        Assert.Equal(14, temp.GetProperty("missed").GetInt32());
        Assert.True(temp.GetProperty("lowConfidence").GetBoolean());

        var highlights = report.GetProperty("highlights").EnumerateArray().Select(h => h.GetString()!).ToList();
        Assert.Contains(highlights, h => h.StartsWith("Systolic blood pressure rose over this period"));
        Assert.Contains("Body temperature: 14 of 14 scheduled measurements missed.", highlights);
    }

    [Fact]
    public async Task First_period_is_marked_as_adaptation_window()
    {
        var a = await CrewId();
        var report = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/current", role: "crew", crew: a);
        Assert.Equal(1, report.GetProperty("period").GetInt32());
        Assert.False(report.GetProperty("complete").GetBoolean());
        var adaptation = report.GetProperty("adaptationWindow");
        Assert.True(adaptation.GetProperty("overlaps").GetBoolean());
        Assert.Equal(14, adaptation.GetProperty("days").GetInt32());
        Assert.Contains(report.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.Contains("adaptation window (mission days 1-14)"));
        Assert.Contains(report.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.StartsWith("Period in progress"));
    }

    [Fact]
    public async Task Ground_report_uses_only_delivered_data()
    {
        var a = await CrewId();
        await Ok(HttpMethod.Post, $"/api/crew/{a}/measurements", new { metricCode = "bp_sys", value = 118 });

        var crewView = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/current", role: "crew", crew: a);
        var groundView = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/current", role: "ground");
        Assert.Equal(1, Row(crewView, "bp_sys").GetProperty("windowN").GetInt32());
        Assert.Equal(0, Row(groundView, "bp_sys").GetProperty("windowN").GetInt32());
        Assert.Equal("ground", groundView.GetProperty("view").GetString());
        Assert.Contains(groundView.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.StartsWith("Ground view: data delivered by"));

        _factory.Time.Advance(TimeSpan.FromSeconds(721));
        groundView = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/current", role: "ground");
        Assert.Equal(1, Row(groundView, "bp_sys").GetProperty("windowN").GetInt32());
    }

    [Fact]
    public async Task Alerts_with_and_without_context_factors_are_counted()
    {
        var a = await CrewId();
        var hrMean = BaselineMean(a, "hr_rest");
        var hrSd = WithDb(db => db.Baselines.Single(b => b.CrewMemberId == a && b.MetricCode == "hr_rest").StandardDeviation);
        // Day 1: z-yellow HR -> adaptation_window factor.
        var y = await Ok(HttpMethod.Post, $"/api/crew/{a}/measurements", new { metricCode = "hr_rest", value = hrMean + 2 * hrSd });
        Assert.NotEmpty(y.GetProperty("context").GetProperty("factors").EnumerateArray());
        // Day 20: red SpO2 with no factor -> unexplained.
        await AdvanceDays(19);
        var red = await Ok(HttpMethod.Post, $"/api/crew/{a}/measurements", new { metricCode = "spo2", value = 85 });
        var redId = red.GetProperty("alertId").GetString();
        // Day 20 symptom alert.
        await Ok(HttpMethod.Post, $"/api/crew/{a}/symptoms", new { symptomCode = "chest_pain" });
        await AdvanceDays(9); // day 29

        var p1 = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/1", role: "crew", crew: a);
        var c1 = p1.GetProperty("members")[0].GetProperty("alerts");
        Assert.Equal(1, c1.GetProperty("measurementAlerts").GetInt32());
        Assert.Equal(1, c1.GetProperty("withContextFactors").GetInt32());
        Assert.Equal(0, c1.GetProperty("withoutContextFactors").GetInt32());

        var p2 = await Ok(HttpMethod.Get, $"/api/crew/{a}/reports/biweekly/2", role: "ground");
        var c2 = p2.GetProperty("members")[0].GetProperty("alerts");
        Assert.Equal(1, c2.GetProperty("withoutContextFactors").GetInt32());
        Assert.Equal(1, c2.GetProperty("symptomAlerts").GetInt32());
        Assert.Equal([redId], c2.GetProperty("unexplainedAlertIds").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(p2.GetProperty("highlights").EnumerateArray(),
            h => h.GetString() == "Alerts: 1 reading alerts, 0 with context factors and 1 without (unexplained); 1 symptom alerts.");
    }

    [Fact]
    public async Task Whole_crew_report_is_ground_only_and_lists_every_member()
    {
        var a = await CrewId();
        var (code, _) = await Send(HttpMethod.Get, "/api/reports/crew/monthly/current", role: "crew", crew: a);
        Assert.Equal(HttpStatusCode.Forbidden, code);

        var report = await Ok(HttpMethod.Get, "/api/reports/crew/monthly/current", role: "ground");
        Assert.Equal("Crew", report.GetProperty("scope").GetString());
        Assert.Equal("Monthly", report.GetProperty("window").GetString());
        Assert.Equal(4, report.GetProperty("members").GetArrayLength());
        Assert.Equal(8, report.GetProperty("members")[0].GetProperty("rows").GetArrayLength()); // every metric, context inputs included
        Assert.Contains(report.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.StartsWith("Across the crew:"));
    }

    [Fact]
    public async Task Crew_cannot_read_another_members_report()
    {
        var a = await CrewId(0);
        var b = await CrewId(1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, $"/api/crew/{b}/reports/biweekly/1", role: "crew", crew: a)).Code);
    }

    [Theory]
    [InlineData("/api/reports/crew/weekly/1", HttpStatusCode.BadRequest, "BAD_WINDOW")]
    [InlineData("/api/reports/crew/biweekly/0", HttpStatusCode.BadRequest, "BAD_PERIOD")]
    [InlineData("/api/reports/crew/biweekly/x", HttpStatusCode.BadRequest, "BAD_PERIOD")]
    [InlineData("/api/reports/crew/biweekly/3", HttpStatusCode.NotFound, "NO_PERIOD")]
    public async Task Bad_report_requests(string url, HttpStatusCode expected, string code)
    {
        var (status, body) = await Send(HttpMethod.Get, url, role: "ground");
        Assert.Equal(expected, status);
        Assert.Equal(code, body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Four_crew_by_90_days_report_generation_under_3_seconds()
    {
        // 4 crew x 90 in-flight days x 8 metrics.
        WithDb(db =>
        {
            var launch = db.Missions.Single().LaunchDate;
            var crew = db.CrewMembers.Select(c => c.Id).ToList();
            var baselines = db.Baselines.ToList();
            var rng = new Random(7);
            var codes = new[] { "hr_rest", "bp_sys", "bp_dia", "temp", "spo2", "sleep_h", "mood", "exercise_min" };
            foreach (var c in crew)
                foreach (var code in codes)
                {
                    var b = baselines.FirstOrDefault(x => x.CrewMemberId == c && x.MetricCode == code);
                    var center = b?.Mean ?? (code == "mood" ? 4 : 60);
                    for (var day = 1; day <= 90; day++)
                    {
                        var at = launch.AddDays(day - 1).AddHours(2.5);
                        db.Measurements.Add(new Measurement
                        {
                            CrewMemberId = c, MetricCode = code, Value = Math.Round(center + rng.NextDouble() - 0.5, 1),
                            RecordedAt = at, Source = HealthMeasurementSource.Inflight, SyncedAt = at,
                        });
                    }
                }
            return 0;
        });
        await AdvanceDays(90); // day 91: 6 biweekly + 3 monthly periods complete

        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ReportService>();
        var sw = Stopwatch.StartNew();
        var stored = await reports.StoreCompletedAsync();
        sw.Stop();
        _out.WriteLine($"Generated {stored} reports (4 crew x 90 days) in {sw.Elapsed.TotalMilliseconds:0} ms");
        Assert.Equal((6 + 3) * 5, stored); // 9 periods x (4 members + crew)
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"report generation took {sw.Elapsed.TotalSeconds:0.00} s");
    }
}
