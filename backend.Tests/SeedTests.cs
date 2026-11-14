using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;
using backend.Entities;
using backend.Services.Seed;

namespace backend.Tests;

// Demo seed (WP 3.5): deterministic, labelled synthetic, and the planted events give the expected
// statuses, trends and context factors.
public sealed class SeedTests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public SeedTests() => _http = _factory.CreateRoleClient();
    public void Dispose() => _factory.Dispose();

    private T Db<T>(Func<ApplicationDbContext, T> f)
    {
        using var scope = _factory.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task<JsonElement> Get(string url, string? role = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (role is not null) req.Headers.Add("X-Role", role);
        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(res.IsSuccessStatusCode, $"{url} -> {(int)res.StatusCode}: {json}");
        return json.GetProperty("data");
    }

    private async Task JumpToDay(double missionDay)
    {
        var launch = Db(db => db.Missions.Single().LaunchDate);
        var now = (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset();
        var target = launch.AddDays(missionDay - 1);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/demo/clock") { Content = JsonContent.Create(new { advanceSeconds = (target - now).TotalSeconds }) };
        (await _http.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private Guid Crew(string firstName) => Db(db => db.CrewMembers.Single(c => c.Name.StartsWith(firstName)).Id);

    private static JsonElement Row(JsonElement report, string metric) =>
        report.GetProperty("members")[0].GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("metricCode").GetString() == metric);

    // Alerts of one crew member with their measurement's metric and mission day.
    private List<(string Metric, int Day, StatusLevel Level, string? Context)> Alerts(Guid crew) => Db(db =>
    {
        var launch = db.Missions.Single().LaunchDate;
        return db.Alerts.Where(a => a.CrewMemberId == crew && a.MeasurementId != null)
            .Join(db.Measurements, a => a.MeasurementId, m => m.Id, (a, m) => new { m.MetricCode, a.CreatedAt, a.Level, a.ContextJson })
            .AsEnumerable()
            .Select(x => (x.MetricCode, (int)Math.Floor((x.CreatedAt - launch).TotalDays) + 1, x.Level, x.ContextJson))
            .ToList();
    });

    [Fact]
    public void Seed_is_labelled_synthetic()
    {
        Db(db =>
        {
            Assert.Contains(DemoSeeder.SyntheticLabel, db.Missions.Single().Name);
            Assert.All(db.CrewMembers.ToList(), c =>
            {
                Assert.EndsWith("(synthetic)", c.Name);
                Assert.StartsWith("SYNTHETIC demo crew member.", c.Notes);
            });
            return 0;
        });
    }

    [Fact]
    public void Shape_4_crew_14_preflight_90_inflight_days_baselines_locked_at_start()
    {
        Db(db =>
        {
            var mission = db.Missions.Single();
            Assert.Equal(MissionMode.Inflight, mission.Mode);
            Assert.Equal(MissionEnvironment.Transit, mission.Environment);
            Assert.Equal(720, mission.OneWayLatencySeconds);
            var crew = db.CrewMembers.ToList();
            Assert.Equal(4, crew.Count);

            foreach (var c in crew)
            {
                var pre = db.Measurements.Count(m => m.CrewMemberId == c.Id && m.MetricCode == "hr_rest" && m.Source == HealthMeasurementSource.Preflight);
                var fly = db.Measurements.Count(m => m.CrewMemberId == c.Id && m.MetricCode == "hr_rest" && m.Source == HealthMeasurementSource.Inflight);
                Assert.Equal(14, pre);
                Assert.Equal(c.Name.StartsWith("Rana") ? 85 : 90, fly); // Rana: 5-day missed streak
                Assert.Equal(8, db.Measurements.Where(m => m.CrewMemberId == c.Id).Select(m => m.MetricCode).Distinct().Count());
            }

            var baselines = db.Baselines.ToList();
            Assert.Equal(24, baselines.Count); // 4 crew x 6 status metrics
            Assert.All(baselines, b =>
            {
                Assert.Equal(14, b.SampleSize);
                Assert.NotNull(b.LockedAt);
                Assert.True(b.LockedAt <= mission.LaunchDate);
            });

            // Reminders are seeded on the crew side and never travel through the link.
            Assert.Equal(24, db.ReminderRules.Count(r => r.CreatedBy == ReminderCreatedBy.Seed));
            Assert.DoesNotContain(db.LinkMessages.ToList(), m => m.Type != LinkMessageType.Alert);
            return 0;
        });
    }

    [Fact]
    public async Task Seed_is_deterministic()
    {
        using var other = new BaselineApiFactory();
        _ = other.CreateClient(); // starts the app, which seeds an empty database
        static List<string> Fingerprint(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var launch = db.Missions.Single().LaunchDate;
            var names = db.CrewMembers.ToDictionary(c => c.Id, c => c.Name);
            var readings = db.Measurements.AsEnumerable()
                .Select(m => $"{names[m.CrewMemberId]}|{m.MetricCode}|{(m.RecordedAt - launch).TotalMinutes}|{m.Value}");
            var alerts = db.Alerts.AsEnumerable()
                .Select(a => $"{names[a.CrewMemberId]}|{(a.CreatedAt - launch).TotalMinutes}|{a.Level}|{a.RuleId}|{a.Reason}");
            var env = db.EnvironmentLogs.AsEnumerable().Select(e => $"{e.Kind}|{(e.RecordedAt - launch).TotalMinutes}|{e.Value}");
            return readings.Concat(alerts).Concat(env).Order().ToList();
        }
        await Task.CompletedTask;
        Assert.Equal(Fingerprint(_factory.Services), Fingerprint(other.Services));
    }

    [Fact]
    public async Task Seeded_history_appears_only_as_the_clock_reaches_it()
    {
        var jordan = Crew("Jordan");
        Assert.Empty((await Get($"/api/crew/{jordan}/alerts")).EnumerateArray());
        Assert.DoesNotContain((await Get($"/api/crew/{jordan}/measurements?days=1")).EnumerateArray(), m => m.GetProperty("source").GetString() == "Inflight");

        await JumpToDay(53);
        var alerts = (await Get($"/api/crew/{jordan}/alerts")).EnumerateArray().ToList();
        Assert.Contains(alerts, a => a.GetProperty("reason").GetString()!.StartsWith("Body temperature 38.6"));
    }

    // Ramp: green for the first third, yellow in the middle, red near the end (both BP metrics).
    [Theory]
    [InlineData("bp_sys")]
    [InlineData("bp_dia")]
    public void Bp_drift_ramps_green_then_yellow_then_red(string metric)
    {
        var alex = Alerts(Crew("Alex")).Where(a => a.Metric == metric).ToList();
        int Count(int from, int to, StatusLevel level) => alex.Count(a => a.Day >= from && a.Day <= to && a.Level == level);

        Assert.True(Count(1, 30, StatusLevel.Yellow) + Count(1, 30, StatusLevel.Red) <= 3, "first third mostly green");
        Assert.Equal(0, Count(1, 30, StatusLevel.Red));
        Assert.True(Count(31, 60, StatusLevel.Yellow) >= 24, "middle third mostly yellow");
        Assert.True(Count(31, 60, StatusLevel.Red) <= 2, "red only near the end");
        Assert.True(Count(61, 90, StatusLevel.Red) >= 24, "last third mostly red");
    }

    [Fact]
    public void Ramp_knots_give_the_intended_sd_levels()
    {
        Assert.Equal(0, DemoSeeder.BpDriftSd(1));
        Assert.True(DemoSeeder.BpDriftSd(27) < 1.5);
        Assert.InRange(DemoSeeder.BpDriftSd(45), 1.5, 2.5);
        Assert.True(DemoSeeder.BpDriftSd(70) > 2.5);
        for (var d = 2; d <= 90; d++) Assert.True(DemoSeeder.BpDriftSd(d) >= DemoSeeder.BpDriftSd(d - 1), "monotone ramp");
    }

    [Fact]
    public async Task Bp_drift_shows_in_the_reports()
    {
        var alex = Crew("Alex");
        await JumpToDay(31.5); // monthly period 1 (days 1-30) complete: the ramp from 0 to ~1.3 SD is a rising trend
        var monthly = await Get($"/api/crew/{alex}/reports/monthly/1", "ground");
        Assert.Equal("rising", Row(monthly, "bp_sys").GetProperty("trend").GetString());
        Assert.Contains(monthly.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.StartsWith("Systolic blood pressure rose over this period"));

        await JumpToDay(91.5); // monthly period 3 (days 61-90): well above baseline, red range
        var m3 = await Get($"/api/crew/{alex}/reports/monthly/3", "ground");
        Assert.True(Row(m3, "bp_sys").GetProperty("zSd").GetDouble() > 2.5);
        Assert.True(Row(m3, "bp_dia").GetProperty("zSd").GetDouble() > 2.5);
        var m2 = await Get($"/api/crew/{alex}/reports/monthly/2", "ground");
        Assert.InRange(Row(m2, "bp_sys").GetProperty("zSd").GetDouble(), 1.5, 2.5);
    }

    [Fact]
    public async Task Sleep_decline_with_mood_dip_shows_in_report_and_factors()
    {
        var sam = Crew("Sam");
        await JumpToDay(43.5);
        var bi3 = await Get($"/api/crew/{sam}/reports/biweekly/3", "ground");
        var sleep = Row(bi3, "sleep_h");
        Assert.Equal("falling", sleep.GetProperty("trend").GetString());
        Assert.True(sleep.GetProperty("zSd").GetDouble() < -1.5);
        Assert.True(Row(bi3, "mood").GetProperty("zSd").GetDouble() < -1.5);

        var alerts = Alerts(sam);
        var sleepFlags = alerts.Where(a => a.Metric == "sleep_h" && a.Day >= 33 && a.Day <= 45).ToList();
        Assert.True(sleepFlags.Count >= 8);
        Assert.Contains(sleepFlags, a => a.Level == StatusLevel.Red);
        // Cabin CO2 entry on day 38: factor on low-sleep flags in the following 72 h.
        Assert.Contains(sleepFlags, a => a.Day >= 38 && a.Day <= 41 && a.Context!.Contains("\"elevated_co2\""));
    }

    [Fact]
    public void Fever_day_gives_red_temperature_and_explained_heart_rate()
    {
        var jordan = Alerts(Crew("Jordan")).Where(a => a.Day == DemoSeeder.FeverDay).ToList();
        Assert.Contains(jordan, a => a.Metric == "temp" && a.Level == StatusLevel.Red);
        var hr = Assert.Single(jordan, a => a.Metric == "hr_rest");
        Assert.Contains("\"recent_fever\"", hr.Context);
    }

    [Fact]
    public async Task Missed_streak_shows_in_report_and_reminders()
    {
        var rana = Crew("Rana");
        await JumpToDay(71.5); // biweekly period 5 (days 57-70) complete
        var bi5 = await Get($"/api/crew/{rana}/reports/biweekly/5", "ground");
        foreach (var metric in new[] { "hr_rest", "bp_sys", "bp_dia", "temp", "spo2", "sleep_h" })
            Assert.Equal(5, Row(bi5, metric).GetProperty("missed").GetInt32());
        Assert.Contains(bi5.GetProperty("highlights").EnumerateArray(), h => h.GetString() == "Resting heart rate: 5 of 14 scheduled measurements missed.");

        // Crew-side reminders: the streak is Missed, the other seeded days are Done.
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/crew/{rana}/reminders?openOnly=false");
        req.Headers.Add("X-Role", "crew");
        req.Headers.Add("X-Crew-Id", rana.ToString());
        var reminders = (await (await _http.SendAsync(req)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(5 * 6, reminders.Count(r => r.GetProperty("status").GetString() == "Missed"));
        Assert.True(reminders.Count(r => r.GetProperty("status").GetString() == "Done") > 60 * 6);
    }

    [Fact]
    public void Space_weather_entries_make_their_factors_fire()
    {
        var alex = Alerts(Crew("Alex"));
        Assert.Contains(alex, a => a.Day == DemoSeeder.SpaceWeatherDay && a.Metric == "bp_sys"
                                   && a.Context!.Contains("\"space_weather_kp\"") && a.Context.Contains("\"space_weather_spe\""));
    }

    [Fact]
    public void Mood_and_exercise_do_not_trip_high_stress_or_missed_exercise_on_most_flags()
    {
        var sam = Crew("Sam");
        var all = Db(db => db.CrewMembers.Select(c => c.Id).ToList()).SelectMany(id => Alerts(id).Select(a => (Crew: id, a.Day, a.Context))).ToList();
        Assert.True(all.Count > 50);
        Assert.DoesNotContain(all, a => a.Context?.Contains("\"missed_exercise\"") == true);
        // high_stress only where the mood dip was planted (crew 2, days 36-46 with the 24 h window).
        var stress = all.Where(a => a.Context?.Contains("\"high_stress\"") == true).ToList();
        Assert.All(stress, a => { Assert.Equal(sam, a.Crew); Assert.InRange(a.Day, DemoSeeder.MoodDipFromDay, DemoSeeder.MoodDipUntilDay + 1); });
        Assert.True(stress.Count < all.Count / 10);
    }
}
