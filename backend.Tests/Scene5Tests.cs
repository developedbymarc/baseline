using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests;

// POST /api/demo/scene5: sets up demo step 5 (fast-forward off, link up, 12 min latency) from any state.
public sealed class Scene5Tests : IDisposable
{
    private readonly BaselineApiFactory _factory = new();
    private readonly HttpClient _http;

    public Scene5Tests() => _http = _factory.CreateRoleClient();
    public void Dispose() => _factory.Dispose();

    private async Task<JsonElement> Post(string url, object? body = null)
    {
        var res = await _http.PostAsJsonAsync(url, body ?? new { });
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(res.IsSuccessStatusCode, $"{url} -> {(int)res.StatusCode}: {json}");
        return json.GetProperty("data");
    }

    private async Task<JsonElement> Get(string url) =>
        (await _http.GetFromJsonAsync<JsonElement>(url)).GetProperty("data");

    private async Task<string> CrewId() => (await Get("/api/crew"))[0].GetProperty("id").GetString()!;

    // The state a presenter is likely in just before step 5: fast-forwarding, paused or not, link down, other latency.
    private async Task MessUpTheDemoState()
    {
        await Post("/api/demo/clock", new { fastForward = true });
        _factory.Time.Advance(TimeSpan.FromSeconds(105)); // 3 mission weeks
        await Post("/api/demo/clock", new { paused = true });
        await Post("/api/demo/link", new { up = false, oneWayLatencySeconds = 60 });
    }

    [Fact]
    public async Task Scene5_sets_real_time_link_up_and_12_min_latency_and_returns_the_clock()
    {
        await MessUpTheDemoState();
        var state = await Post("/api/demo/scene5");

        Assert.Equal(1, state.GetProperty("clockRate").GetDouble());
        Assert.False(state.GetProperty("clockPaused").GetBoolean());
        Assert.True(state.GetProperty("linkUp").GetBoolean());
        Assert.Equal(720, state.GetProperty("oneWayLatencySeconds").GetDouble());
        Assert.True(state.GetProperty("onboardFirst").GetBoolean()); // 12 min > 5 min: on-board protocol first
        Assert.Matches(@"^21d 00:\d\d:\d\d$", state.GetProperty("met").GetString());

        // The returned clock time is the mission time, and from now on it runs at real speed.
        var returned = state.GetProperty("missionNow").GetDateTimeOffset();
        Assert.Equal(returned, (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset());
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(returned.AddMinutes(1), (await Get("/api/mission")).GetProperty("missionNow").GetDateTimeOffset());
    }

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task Chest_pain_window_works_right_after_scene5(int minutesBetween, bool offered)
    {
        await MessUpTheDemoState();
        await Post("/api/demo/scene5");
        var crew = await CrewId();

        var symptom = await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        Assert.Equal("chest_pain", symptom.GetProperty("protocolId").GetString());
        _factory.Time.Advance(TimeSpan.FromMinutes(minutesBetween)); // real minutes = mission minutes after scene5

        var red = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 130 });
        Assert.Equal("Red", red.GetProperty("status").GetProperty("level").GetString());
        if (offered) Assert.Equal("chest_pain", red.GetProperty("protocolId").GetString());
        else Assert.Equal(JsonValueKind.Null, red.GetProperty("protocolId").ValueKind);
    }

    [Fact]
    public async Task Packet_sent_after_scene5_arrives_after_12_minutes()
    {
        await MessUpTheDemoState();
        await Post("/api/demo/scene5");
        var crew = await CrewId();
        var red = await Post($"/api/crew/{crew}/measurements", new { metricCode = "hr_rest", value = 130 });
        var msg = await Post($"/api/crew/{crew}/packets", new { alertId = red.GetProperty("alertId").GetString() });
        Assert.Equal("in_flight", msg.GetProperty("state").GetString());
        Assert.Equal(msg.GetProperty("sentAt").GetDateTimeOffset().AddMinutes(12), msg.GetProperty("deliverAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Messages_held_before_scene5_leave_when_it_brings_the_link_up()
    {
        await Post("/api/demo/link", new { up = false });
        var crew = await CrewId();
        await Post($"/api/crew/{crew}/symptoms", new { symptomCode = "chest_pain" });
        Assert.All((await Get("/api/link/queue")).EnumerateArray(), m => Assert.Equal("held", m.GetProperty("state").GetString()));

        var state = await Post("/api/demo/scene5");
        var now = state.GetProperty("missionNow").GetDateTimeOffset();
        Assert.All((await Get("/api/link/queue")).EnumerateArray(), m =>
        {
            Assert.Equal("in_flight", m.GetProperty("state").GetString());
            Assert.Equal(now.AddMinutes(12), m.GetProperty("deliverAt").GetDateTimeOffset());
        });
    }
}
