using backend.Configuration;
using backend.Entities;

namespace backend.Tests;

public class HealthConfigTests
{
    [Fact]
    public void Real_config_files_load_and_validate()
    {
        var c = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);
        foreach (var code in new[] { "hr_rest", "bp_sys", "bp_dia", "temp", "spo2", "sleep_h", "mood", "exercise_min" })
            Assert.True(c.Metrics.ContainsKey(code), code);
        Assert.True(c.Metrics["hr_rest"].HasStatus);
        Assert.False(c.Metrics["mood"].HasStatus);
        Assert.Null(c.Metrics["spo2"].WarnHigh);
    }

    [Fact]
    public void Chest_pain_protocol_tree_is_complete()
    {
        var p = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Protocols["chest_pain"];
        Assert.Equal("n_start", p.Start);
        var start = p.TreeYaml.Single(n => n.Id == "n_start");
        Assert.Equal(2, start.Options.Count);
        Assert.Contains(p.TreeYaml, n => n.Type == "end");
        Assert.Equal(600, p.TreeYaml.Single(n => n.Id == "n_recheck").TimerS);
        Assert.True(p.TreeYaml.Single(n => n.Id == "n_notify").BuildPacket);
    }

    private static HealthConfig With(HealthMetric m, ProtocolDefinition? p = null) =>
        new([m], p is null ? [] : [p]);

    [Fact]
    public void Rejects_z_warn_not_below_z_crit()
    {
        var m = TestMetrics.HeartRate();
        m.ZWarn = 3;
        Assert.Throws<ConfigValidationException>(() => With(m).Validate());
    }

    [Fact]
    public void Rejects_inverted_limits_and_plausible_range()
    {
        var m = TestMetrics.HeartRate();
        m.CritHigh = 90; // below warn_high 100
        Assert.Throws<ConfigValidationException>(() => With(m).Validate());

        var p = TestMetrics.HeartRate();
        p.PlausibleMin = 300;
        Assert.Throws<ConfigValidationException>(() => With(p).Validate());
    }

    [Fact]
    public void Rejects_unknown_polarity()
    {
        var m = TestMetrics.HeartRate("sideways");
        Assert.Throws<ConfigValidationException>(() => With(m).Validate());
    }

    [Fact]
    public void Context_metric_without_limits_is_valid()
    {
        With(TestMetrics.Context()).Validate();
    }

    private static ProtocolDefinition Tree(params ProtocolNode[] nodes) => new()
    {
        Id = "p", Title = "P", Version = "1", Start = "a", TreeYaml = nodes.ToList(),
    };

    private static void AssertInvalid(HealthConfig c, string expectedFragment)
    {
        var ex = Assert.Throws<ConfigValidationException>(c.Validate);
        Assert.Contains(expectedFragment, ex.Message);
    }

    private static ProtocolNode End(string id = "b", string outcome = "done") =>
        new() { Id = id, Type = "end", Text = "done", Outcome = outcome, Src = ["team"] };

    [Fact]
    public void Rejects_dangling_next_and_unknown_metric_and_source()
    {
        AssertInvalid(With(TestMetrics.HeartRate(), Tree(
            new ProtocolNode { Id = "a", Type = "action", Text = "x", Next = "missing" })),
            "next 'missing' does not exist");

        AssertInvalid(With(TestMetrics.HeartRate(), Tree(
            new ProtocolNode { Id = "a", Type = "measure", Text = "x", Next = "b", Metrics = ["nope"] },
            End())),
            "unknown metric 'nope'");

        AssertInvalid(With(TestMetrics.HeartRate(), Tree(
            new ProtocolNode { Id = "a", Type = "end", Text = "y", Outcome = "o", Src = ["S9"] })),
            "unknown source 'S9'");
    }

    [Fact]
    public void Accepts_minimal_valid_tree()
    {
        var ok = Tree(
            new ProtocolNode { Id = "a", Type = "question", Text = "?", Options = [new() { Label = "Y", Next = "b" }] },
            End());
        With(TestMetrics.HeartRate(), ok).Validate();
    }

    [Fact]
    public void Missing_directory_fails_fast()
    {
        Assert.Throws<ConfigValidationException>(() => HealthConfig.LoadFromDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }
}
