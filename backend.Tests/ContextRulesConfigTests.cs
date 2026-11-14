using backend.Configuration;
using backend.Entities;

namespace backend.Tests;

public class ContextRulesConfigTests
{
    private static HealthConfig Real => HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);

    [Fact]
    public void Real_context_rules_load()
    {
        var cr = Real.ContextRules!;
        Assert.Equal("0.3", cr.Version);
        Assert.Equal(1, cr.Policy!.MaxTotalDowngrade);
        Assert.Equal("yellow", cr.Policy.DowngradeOnlyFrom);
        Assert.Equal(["temp", "spo2"], cr.Policy.NeverDowngradeMetrics);
        Assert.Equal(
            ["short_sleep", "missed_exercise", "adaptation_window", "space_weather_kp", "space_weather_spe", "elevated_co2", "high_stress", "recent_fever"],
            cr.Rules.Select(r => r.Id));
        var spe = cr.Rules.Single(r => r.Id == "space_weather_spe").Condition!;
        Assert.Null(spe.Op);
        Assert.Null(spe.Value);
        Assert.Equal(24, spe.WindowH);
    }

    [Fact]
    public void Review_true_rules_are_startup_warnings()
    {
        var w = Real.Warnings;
        foreach (var id in new[] { "missed_exercise", "adaptation_window", "space_weather_kp", "space_weather_spe" })
            Assert.Contains(w, x => x.Contains($"rule '{id}'") && x.Contains("review: true"));
        foreach (var id in new[] { "short_sleep", "elevated_co2", "high_stress", "recent_fever" })
            Assert.DoesNotContain(w, x => x.Contains($"rule '{id}'"));
    }

    [Fact]
    public void Missing_file_is_a_warning_not_an_error()
    {
        var c = new HealthConfig([TestMetrics.HeartRate()], []);
        c.Validate();
        Assert.Null(c.ContextRules);
        Assert.Contains(c.Warnings, w => w.Contains("context_rules.yaml not found"));
    }

    // ---------- Strict validation, table-driven ----------

    private static ContextRulesFile Valid() => new()
    {
        Version = "1",
        Policy = new ContextPolicy { MaxTotalDowngrade = 1, DowngradeOnlyFrom = "yellow", NeverDowngradeMetrics = ["temp_test"] },
        Sources = [new ContextSource { Id = "C1", Publisher = "P", Opened = true }],
        Rules =
        [
            new ContextRule
            {
                Id = "r1", Enabled = true, AppliesTo = ["hr_test"], Direction = "high", Message = "m",
                Source = ["C1"], SeverityModifier = -1, Review = false,
                Condition = new ContextCondition { Type = "metric_count", Metric = "hr_test", Op = "lt", Value = 6, WindowDays = 3, MinCount = 2 },
            },
        ],
    };

    private static HealthConfig With(ContextRulesFile cr) =>
        new([TestMetrics.HeartRate(), TestMetrics.Temperature(), TestMetrics.Context()], [], "1", cr);

    [Fact]
    public void Valid_baseline_file_passes() => With(Valid()).Validate();

    public static TheoryData<string, Action<ContextRulesFile>, string> Invalid => new()
    {
        { "no version", f => f.Version = null, "version is required" },
        { "no policy", f => f.Policy = null, "policy is required" },
        { "downgrade 2", f => f.Policy!.MaxTotalDowngrade = 2, "max_total_downgrade must be 0 or 1" },
        { "downgrade from green", f => f.Policy!.DowngradeOnlyFrom = "green", "downgrade_only_from must be yellow or red" },
        { "never metric", f => f.Policy!.NeverDowngradeMetrics = ["nope"], "never_downgrade_metrics: unknown metric 'nope'" },
        { "no rules", f => f.Rules.Clear(), "rules must not be empty" },
        { "no id", f => f.Rules[0].Id = null, "id is required" },
        { "dup id", f => f.Rules.Add(Clone(f.Rules[0])), "duplicate id" },
        { "no enabled", f => f.Rules[0].Enabled = null, "enabled is required" },
        { "no review", f => f.Rules[0].Review = null, "review is required" },
        { "no message", f => f.Rules[0].Message = " ", "message is required" },
        { "bad direction", f => f.Rules[0].Direction = "up", "direction must be high, low or any" },
        { "modifier -2", f => f.Rules[0].SeverityModifier = -2, "severity_modifier must be 0 or -1" },
        { "modifier missing", f => f.Rules[0].SeverityModifier = null, "severity_modifier must be 0 or -1" },
        { "applies empty", f => f.Rules[0].AppliesTo = [], "applies_to must list at least one metric" },
        { "applies unknown", f => f.Rules[0].AppliesTo = ["glucose"], "applies_to: unknown metric 'glucose'" },
        { "applies context", f => f.Rules[0].AppliesTo = ["mood_test"], "is never flagged" },
        { "source unknown", f => f.Rules[0].Source = ["C9"], "unknown source 'C9'" },
        { "source empty", f => f.Rules[0].Source = [], "source must list at least one id" },
        { "no condition", f => f.Rules[0].Condition = null, "condition is required" },
        { "bad type", f => f.Rules[0].Condition!.Type = "trend", "condition.type 'trend'" },
        { "bad op", f => f.Rules[0].Condition!.Op = "eq", "condition.op 'eq'" },
        { "count no min", f => f.Rules[0].Condition!.MinCount = null, "needs min_count" },
        { "count min 0", f => f.Rules[0].Condition!.MinCount = 0, "min_count must be at least 1" },
        { "count no window", f => f.Rules[0].Condition!.WindowDays = null, "needs window_days" },
        { "count window 0", f => f.Rules[0].Condition!.WindowDays = 0, "window_days must be a whole number of mission days" },
        { "count window 2.5", f => f.Rules[0].Condition!.WindowDays = 2.5, "window_days must be a whole number of mission days" },
        { "count bad metric", f => f.Rules[0].Condition!.Metric = "nope", "condition.metric: unknown metric 'nope'" },
        { "count extra field", f => f.Rules[0].Condition!.WindowH = 4, "does not use window_h" },
        { "day extra metric", f => f.Rules[0].Condition = new() { Type = "mission_day", Op = "lte", Value = 14, Metric = "hr_test" }, "does not use metric" },
        { "day no value", f => f.Rules[0].Condition = new() { Type = "mission_day", Op = "lte" }, "needs value" },
        { "env no op", f => f.Rules[0].Condition = new() { Type = "environment", Kind = "kp", Value = 5, WindowH = 24 }, "needs op" },
        { "env no window", f => f.Rules[0].Condition = new() { Type = "environment", Kind = "kp", Op = "gte", Value = 5 }, "needs window_h" },
        { "env window 0", f => f.Rules[0].Condition = new() { Type = "environment", Kind = "kp", Op = "gte", Value = 5, WindowH = 0 }, "window_h must be above 0" },
        { "env bad kind", f => f.Rules[0].Condition = new() { Type = "environment", Kind = "o2", Op = "gte", Value = 5, WindowH = 24 }, "condition.kind 'o2'" },
        { "spe with value", f => f.Rules[0].Condition = new() { Type = "environment", Kind = "solar_particle_event", Op = "gte", Value = 1, WindowH = 24 }, "does not use op" },
        { "recent no metric", f => f.Rules[0].Condition = new() { Type = "metric_recent", Op = "lte", Value = 2, WindowH = 24 }, "needs metric" },
        { "recent days", f => f.Rules[0].Condition = new() { Type = "metric_recent", Metric = "hr_test", Op = "lte", Value = 2, WindowH = 24, WindowDays = 1 }, "does not use window_days" },
        { "dup source", f => f.Sources.Add(new ContextSource { Id = "C1" }), "duplicate source id 'C1'" },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_files_are_rejected(string name, Action<ContextRulesFile> mutate, string expected)
    {
        _ = name;
        var f = Valid();
        mutate(f);
        var ex = Assert.Throws<ConfigValidationException>(() => With(f).Validate());
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Spe_without_op_and_value_is_valid_and_team_source_is_allowed()
    {
        var f = Valid();
        f.Rules[0].Condition = new() { Type = "environment", Kind = "solar_particle_event", WindowH = 24 };
        f.Rules[0].Source = ["team"];
        With(f).Validate();
    }

    private static ContextRule Clone(ContextRule r) => new()
    {
        Id = r.Id, Enabled = r.Enabled, AppliesTo = r.AppliesTo, Direction = r.Direction, Condition = r.Condition,
        Message = r.Message, Source = r.Source, SeverityModifier = r.SeverityModifier, Review = r.Review,
    };

    // ---------- Strict loading from disk ----------

    private static void WithConfigCopy(Func<string, string> editRules, Action<string> act)
    {
        var dir = Directory.CreateTempSubdirectory("baseline-ctx-");
        try
        {
            foreach (var f in Directory.GetFiles(TestPaths.ConfigDirectory, "*.yaml"))
                File.Copy(f, Path.Combine(dir.FullName, Path.GetFileName(f)));
            var path = Path.Combine(dir.FullName, "context_rules.yaml");
            File.WriteAllText(path, editRules(File.ReadAllText(path)));
            act(dir.FullName);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void Unknown_key_is_an_error()
    {
        WithConfigCopy(t => t.Replace("    severity_modifier: -1\n    review: false", "    severity_modifer: -1\n    review: false"), dir =>
        {
            var ex = Assert.Throws<ConfigValidationException>(() => HealthConfig.LoadFromDirectory(dir));
            Assert.Contains("context_rules.yaml line", ex.Message);
        });
    }

    [Fact]
    public void Unquoted_version_still_loads_as_text()
    {
        var current = Real.ContextRules!.Version!;
        WithConfigCopy(t => t.Replace($"version: \"{current}\"", $"version: {current}"), dir =>
            Assert.Equal(current, HealthConfig.LoadFromDirectory(dir).ContextRules!.Version));
    }

    // ---------- chest_pain trigger.window_min ----------

    [Fact]
    public void Chest_pain_window_min_loads()
    {
        Assert.Equal(30, Real.Protocols["chest_pain"].Trigger!.WindowMin);
    }

    private static ProtocolDefinition TriggerProtocol(ProtocolTrigger t) => new()
    {
        Id = "p", Title = "P", Version = "1", Start = "a", Trigger = t,
        TreeYaml = [new ProtocolNode { Id = "a", Type = "end", Text = "t", Outcome = "o" }],
    };

    [Theory]
    [InlineData(0, "window_min must be a whole number of minutes above 0")]
    [InlineData(-5, "window_min must be a whole number of minutes above 0")]
    [InlineData(null, "window_min is required when requires_symptom is true")]
    public void Window_min_must_be_positive_and_present(int? window, string expected)
    {
        var p = TriggerProtocol(new ProtocolTrigger { Symptoms = ["x"], RedMetrics = ["hr_test"], RequiresSymptom = true, WindowMin = window });
        var ex = Assert.Throws<ConfigValidationException>(() => new HealthConfig([TestMetrics.HeartRate()], [p]).Validate());
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Non_integer_window_min_is_rejected_at_load()
    {
        var dir = Directory.CreateTempSubdirectory("baseline-win-");
        try
        {
            foreach (var f in Directory.GetFiles(TestPaths.ConfigDirectory, "*.yaml"))
                File.Copy(f, Path.Combine(dir.FullName, Path.GetFileName(f)));
            var path = Path.Combine(dir.FullName, "chest_pain.yaml");
            File.WriteAllText(path, File.ReadAllText(path).Replace("window_min: 30", "window_min: 30.5"));
            var ex = Assert.Throws<ConfigValidationException>(() => HealthConfig.LoadFromDirectory(dir.FullName));
            Assert.Contains("chest_pain.yaml line", ex.Message);
        }
        finally { dir.Delete(true); }
    }
}
