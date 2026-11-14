using backend.Configuration;
using backend.Entities;

namespace backend.Tests;

// metrics.yaml v1.2 / chest_pain.yaml v0.2 schema: version strings, fields lists, sd_pop,
// structured trigger, end outcomes, and warnings that never block startup.
public class ConfigSchemaTests
{
    private static HealthConfig Real => HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);

    // ---------- Real files ----------

    [Fact]
    public void Versions_are_read_as_strings()
    {
        var c = Real;
        Assert.Equal("1.2", c.MetricsVersion);
        Assert.Equal("0.3", c.Protocols["chest_pain"].Version);
    }

    [Fact]
    public void Source_fields_are_lists()
    {
        var bp = Real.Metrics["bp_sys"];
        Assert.Equal(["normal_high", "warn_high", "crit_high"], bp.Sources[0].Fields);
        Assert.Contains(bp.Sources, s => s.Publisher == "team" && s.Fields.Contains("z_warn"));
        Assert.Contains(bp.Sources, s => s.Fields.SequenceEqual(["sd_pop"]) && s.Opened == false);
    }

    [Fact]
    public void Context_inputs_with_null_limits_and_polarity_none_load()
    {
        foreach (var code in new[] { "mood", "exercise_min" })
        {
            var m = Real.Metrics[code];
            Assert.Equal("none", m.Polarity);
            Assert.False(m.HasStatus);
            Assert.Null(m.WarnLow);
            Assert.Null(m.CritHigh);
            Assert.Null(m.ZWarn);
            Assert.Null(m.SdPop);
            Assert.Single(m.Sources);
            Assert.Null(m.Sources[0].Opened); // team entry
        }
    }

    [Fact]
    public void Sd_pop_is_nullable_per_metric()
    {
        var c = Real;
        Assert.Equal(19.34, c.Metrics["bp_sys"].SdPop);
        Assert.Equal(12.10, c.Metrics["bp_dia"].SdPop);
        Assert.Null(c.Metrics["hr_rest"].SdPop);
        Assert.Null(c.Metrics["spo2"].SdPop);
    }

    [Fact]
    public void Chest_pain_trigger_is_structured()
    {
        var t = Real.Protocols["chest_pain"].Trigger!;
        Assert.Equal(["chest_pain"], t.Symptoms);
        Assert.Equal(["hr_rest", "bp_sys", "bp_dia", "spo2"], t.RedMetrics);
        Assert.True(t.RequiresSymptom);
        Assert.Equal("chest_pain", Real.ProtocolForSymptom("chest_pain")!.Id);
        Assert.Null(Real.ProtocolForSymptom("headache"));
    }

    [Fact]
    public void Latency_rule_is_display_text_only()
    {
        Assert.False(string.IsNullOrWhiteSpace(Real.Protocols["chest_pain"].LatencyRule));
    }

    [Fact]
    public void Chest_pain_end_outcomes()
    {
        Assert.Equal(new HashSet<string> { "emergency_cpr", "hold_and_wait_for_ground" },
            HealthConfig.Outcomes(Real.Protocols["chest_pain"]));
    }

    // ---------- Warnings, not errors ----------

    [Fact]
    public void Real_config_loads_with_warnings_for_unopened_sources_review_nodes_and_missing_sd_pop()
    {
        var w = Real.Warnings;
        Assert.Contains(w, x => x.Contains("protocol 'chest_pain'") && x.Contains("source S5 has opened: false"));
        Assert.Contains(w, x => x.Contains("metric 'bp_sys'") && x.Contains("opened: false"));
        Assert.Contains(w, x => x.Contains("node 'n_aspirin'") && x.Contains("review: true"));
        Assert.Contains(w, x => x.Contains("node 'n_cpr'") && x.Contains("review: true"));
        Assert.Contains(w, x => x.Contains("metric 'hr_rest'") && x.Contains("sd_pop is null"));
        Assert.DoesNotContain(w, x => x.Contains("metric 'bp_sys'") && x.Contains("sd_pop is null"));
        Assert.DoesNotContain(w, x => x.Contains("metric 'mood'") && x.Contains("sd_pop"));
    }

    [Fact]
    public void Opened_false_and_review_true_never_fail_validation()
    {
        var m = TestMetrics.HeartRate();
        m.Sources = [new MetricSource { Publisher = "X", Fields = ["warn_low"], Opened = false }];
        var p = Protocol(
            new ProtocolNode { Id = "a", Type = "end", Text = "t", Outcome = "o", Review = true });
        p.Sources = [new ProtocolSource { Id = "S1", Opened = false }];
        var c = new HealthConfig([m], [p]);
        c.Validate();
        // sd_pop null, metric source, review node, protocol source (plus "context_rules.yaml not found")
        Assert.Equal(4, c.Warnings.Count(w => !w.Contains("context_rules.yaml")));
    }

    [Fact]
    public void Startup_from_files_with_unquoted_version_and_warnings()
    {
        var dir = Directory.CreateTempSubdirectory("baseline-cfg-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "metrics.yaml"), """
                version: 1.3
                metrics:
                  - code: hr
                    name: HR
                    unit: bpm
                    plausible_min: 25
                    plausible_max: 220
                    warn_low: 50
                    warn_high: 100
                    crit_low: 40
                    crit_high: 120
                    z_warn: 1.5
                    z_crit: 2.5
                    sd_pop: null
                    polarity: both
                    sources:
                      - fields: [warn_low, warn_high]
                        publisher: Somebody
                        opened: false
                """);
            var c = HealthConfig.LoadFromDirectory(dir.FullName);
            Assert.Equal("1.3", c.MetricsVersion);
            Assert.Contains(c.Warnings, w => w.Contains("opened: false"));
        }
        finally { dir.Delete(true); }
    }

    // ---------- Errors ----------

    private static ProtocolDefinition Protocol(params ProtocolNode[] nodes) => new()
    {
        Id = "p", Title = "P", Version = "1", Start = nodes[0].Id, TreeYaml = nodes.ToList(),
    };

    private static ProtocolNode End(string id = "z", string? outcome = "done") =>
        new() { Id = id, Type = "end", Text = "t", Outcome = outcome };

    private static void AssertInvalid(HealthConfig c, string fragment)
    {
        var ex = Assert.Throws<ConfigValidationException>(c.Validate);
        Assert.Contains(fragment, ex.Message);
    }

    [Fact]
    public void Source_with_unknown_or_no_fields_is_rejected()
    {
        var m = TestMetrics.HeartRate();
        m.Sources = [new MetricSource { Publisher = "X", Fields = ["warn_hihg"] }];
        AssertInvalid(new HealthConfig([m], []), "unknown field 'warn_hihg'");

        m.Sources = [new MetricSource { Publisher = "X", Fields = [] }];
        AssertInvalid(new HealthConfig([m], []), "must list the fields");
    }

    [Fact]
    public void Missing_version_is_rejected()
    {
        AssertInvalid(new HealthConfig([TestMetrics.HeartRate()], [], metricsVersion: ""), "version is required");
        var p = Protocol(End());
        p.Version = "";
        AssertInvalid(new HealthConfig([TestMetrics.HeartRate()], [p]), "protocol 'p': version is required");
    }

    [Fact]
    public void Non_positive_sd_pop_is_rejected()
    {
        var m = TestMetrics.HeartRate(sdPop: 0);
        AssertInvalid(new HealthConfig([m], []), "sd_pop must be positive");
    }

    [Fact]
    public void Trigger_rules()
    {
        var hr = TestMetrics.HeartRate();
        var mood = TestMetrics.Context();

        var p = Protocol(End());
        p.Trigger = new ProtocolTrigger { RedMetrics = ["nope"] };
        AssertInvalid(new HealthConfig([hr], [p]), "unknown metric 'nope'");

        p.Trigger = new ProtocolTrigger { Symptoms = ["x"], RedMetrics = ["mood_test"] };
        AssertInvalid(new HealthConfig([hr, mood], [p]), "can never be red");

        p.Trigger = new ProtocolTrigger { RedMetrics = ["hr_test"], RequiresSymptom = true };
        AssertInvalid(new HealthConfig([hr], [p]), "requires_symptom: true but no symptoms");

        p.Trigger = new ProtocolTrigger();
        AssertInvalid(new HealthConfig([hr], [p]), "trigger needs symptoms or red_metrics");

        p.Trigger = new ProtocolTrigger { Symptoms = ["x"], RedMetrics = ["hr_test"], RequiresSymptom = true, WindowMin = 30 };
        new HealthConfig([hr], [p]).Validate();
    }

    [Fact]
    public void End_node_outcome_rules()
    {
        var hr = TestMetrics.HeartRate();
        AssertInvalid(new HealthConfig([hr], [Protocol(End(outcome: null))]), "end node needs an outcome");
        AssertInvalid(new HealthConfig([hr], [Protocol(End(outcome: " "))]), "end node needs an outcome");

        var actionWithOutcome = Protocol(
            new ProtocolNode { Id = "a", Type = "action", Text = "t", Next = "z", Outcome = "x" }, End());
        AssertInvalid(new HealthConfig([hr], [actionWithOutcome]), "only end nodes may have an outcome");

        var duplicate = Protocol(
            new ProtocolNode { Id = "a", Type = "question", Text = "?", Options = [new() { Label = "1", Next = "y" }, new() { Label = "2", Next = "z" }] },
            End("y", "same"), End("z", "same"));
        AssertInvalid(new HealthConfig([hr], [duplicate]), "outcome 'same' is used by more than one end node");
    }
}
