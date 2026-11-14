using backend.Configuration;
using backend.Entities;
using backend.Services.Engines;

namespace backend.Tests;

// Table-driven tests of every rule in the real config/context_rules.yaml, then the policy block.
public class ContextEngineTests
{
    private static ContextRulesFile Rules() => HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).ContextRules!;

    private static readonly DateTimeOffset Launch = new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);
    // Mission day 21: outside the adaptation window unless a case says otherwise.
    private static readonly DateTimeOffset At = Launch.AddDays(20).AddHours(3);

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private static ContextReading R(string metric, double value, TimeSpan ago) => new(metric, value, At - ago);
    // A reading on mission day N (day 1 starts at launch), `hour` hours into that day.
    private static ContextReading Day(string metric, double value, int day, double hour = 12) =>
        new(metric, value, Launch.AddDays(day - 1).AddHours(hour));
    // The last second of mission day N.
    private static ContextReading DayEnd(string metric, double value, int day) =>
        new(metric, value, Launch.AddDays(day).AddSeconds(-1));
    private static ContextEnvironmentEntry E(string kind, double value, TimeSpan ago) => new(kind, value, At - ago);
    private static TimeSpan D(double days) => TimeSpan.FromDays(days);
    private static TimeSpan H(double hours) => TimeSpan.FromHours(hours);

    private static ContextResult Run(
        string metric, StatusLevel level, string? direction, ContextReading[]? readings = null,
        ContextEnvironmentEntry[]? env = null, DateTimeOffset? at = null, ContextRulesFile? rules = null) =>
        ContextEngine.Evaluate(rules ?? Rules(), metric, level, direction, at ?? At, Launch, readings ?? [], env ?? []);

    // ---------- One row per rule: fires / does not fire / window edges ----------

    public sealed record Case(string Rule, string Metric, string Direction, ContextReading[] Readings, ContextEnvironmentEntry[] Env, bool Fires)
    {
        public override string ToString() => $"{Rule}: {(Fires ? "fires" : "quiet")} {string.Join(",", Readings.Select(r => $"{r.MetricCode}={r.Value}@{(At - r.RecordedAt).TotalHours:0.###}h"))}{string.Join(",", Env.Select(e => $"{e.Kind}={e.Value}@{(At - e.RecordedAt).TotalHours:0.###}h"))}";
    }

    public static TheoryData<Case> RuleCases => new()
    {
        // short_sleep: sleep_h < 6 on >= 2 DISTINCT MISSION DAYS of the last 3 (days 19-21; now = day 21, 03:00)
        new("short_sleep", "bp_sys", "high", [Day("sleep_h", 5.5, 20), Day("sleep_h", 5, 19)], [], true),          // two short nights, two days = 2
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 21, 1), Day("sleep_h", 5.9, 19)], [], true),      // today counts
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 20), Day("sleep_h", 5, 19, 0)], [], true),        // edge: first second of day 3
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 20), DayEnd("sleep_h", 5, 18)], [], false),       // edge: last second of day 4
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 20), Day("sleep_h", 5, 18)], [], false),          // day 4 is outside
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5.5, 20)], [], false),                               // one night only
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 20, 1), Day("sleep_h", 4, 20, 9)], [], false),     // two short readings, one day = 1
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 6, 20), Day("sleep_h", 6, 19)], [], false),          // 6 is not < 6
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 21, 5), Day("sleep_h", 5, 20)], [], false),       // later today = future, ignored
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 5, 20, 1), Day("sleep_h", 7, 20, 9), Day("sleep_h", 5, 19)], [], false), // latest of day 20 is 7
        new("short_sleep", "hr_rest", "high", [Day("sleep_h", 7, 20, 1), Day("sleep_h", 5, 20, 9), Day("sleep_h", 5, 19)], [], true),  // latest of day 20 is 5

        // missed_exercise: exercise_min < 30 on >= 2 distinct mission days of the last 3; hr_rest high
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 10, 20), Day("exercise_min", 29, 19)], [], true),
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 10, 20, 1), Day("exercise_min", 0, 20, 8)], [], false), // two entries, one day = 1
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 10, 20), Day("exercise_min", 30, 19)], [], false),
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 0, 20), Day("exercise_min", 0, 19)], [], true),          // day 3 inside
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 0, 20), Day("exercise_min", 0, 18)], [], false),         // day 4 outside
        new("missed_exercise", "hr_rest", "high", [Day("exercise_min", 0, 20, 1), Day("exercise_min", 45, 20, 18), Day("exercise_min", 0, 19)], [], false), // made it up later that day

        // space_weather_kp: kp >= 5 in the last 24 h; any direction
        new("space_weather_kp", "sleep_h", "low", [], [E("kp", 5, H(24))], true),        // edge: exactly 24 h, exactly 5
        new("space_weather_kp", "bp_dia", "high", [], [E("kp", 7, H(1))], true),
        new("space_weather_kp", "hr_rest", "high", [], [E("kp", 4.9, H(1))], false),
        new("space_weather_kp", "hr_rest", "high", [], [E("kp", 6, H(24) + Second)], false),

        // space_weather_spe: any active event in the last 24 h, no value needed
        new("space_weather_spe", "hr_rest", "low", [], [E("solar_particle_event", 0, H(24))], true),
        new("space_weather_spe", "hr_rest", "high", [], [E("solar_particle_event", 1, H(24) + Second)], false),
        new("space_weather_spe", "hr_rest", "high", [], [E("kp", 9, H(1))], false),        // other kind does not count

        // elevated_co2: co2_mmhg >= 3.0 in the last 72 h; sleep_h low
        new("elevated_co2", "sleep_h", "low", [], [E("co2_mmhg", 3.0, H(72))], true),
        new("elevated_co2", "sleep_h", "low", [], [E("co2_mmhg", 2.99, H(1))], false),
        new("elevated_co2", "sleep_h", "low", [], [E("co2_mmhg", 5, H(72) + Second)], false),

        // high_stress: a mood rating <= 2 in the last 24 h (5 = best, 1 = worst); hr_rest/bp high
        new("high_stress", "bp_dia", "high", [R("mood", 2, H(24))], [], true),
        new("high_stress", "hr_rest", "high", [R("mood", 1, H(2))], [], true),
        new("high_stress", "hr_rest", "high", [R("mood", 3, H(2))], [], false),
        new("high_stress", "hr_rest", "high", [R("mood", 1, H(24) + Second)], [], false),

        // recent_fever: temp >= 37.5 in the last 24 h; hr_rest high
        new("recent_fever", "hr_rest", "high", [R("temp", 37.5, H(24))], [], true),
        new("recent_fever", "hr_rest", "high", [R("temp", 37.4, H(1))], [], false),
        new("recent_fever", "hr_rest", "high", [R("temp", 39, H(24) + Second)], [], false),
    };

    [Theory]
    [MemberData(nameof(RuleCases))]
    public void Each_rule_fires_only_inside_its_condition(Case c)
    {
        var r = Run(c.Metric, StatusLevel.Yellow, c.Direction, c.Readings, c.Env);
        Assert.Equal(c.Fires, r.Factors.Any(f => f.RuleId == c.Rule));
    }

    [Theory]
    [InlineData(0, true)]            // launch: mission day 1
    [InlineData(13.99999, true)]     // still day 14
    [InlineData(14, false)]          // day 15
    [InlineData(30, false)]
    public void Adaptation_window_covers_mission_days_1_to_14(double daysAfterLaunch, bool fires)
    {
        var at = Launch + TimeSpan.FromDays(daysAfterLaunch);
        var r = Run("hr_rest", StatusLevel.Yellow, "low", at: at);
        Assert.Equal(fires, r.Factors.Any(f => f.RuleId == "adaptation_window"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(23.99, 1)]
    [InlineData(24, 2)]
    [InlineData(-1, 0)]     // before launch
    public void Mission_day_numbering(double hoursAfterLaunch, int day)
    {
        Assert.Equal(day, ContextEngine.MissionDay(Launch, Launch.AddHours(hoursAfterLaunch)));
    }

    [Fact]
    public void Metric_not_in_applies_to_gets_no_factor()
    {
        // recent_fever applies to hr_rest only, not bp_sys.
        var r = Run("bp_sys", StatusLevel.Yellow, "high", [R("temp", 39, H(1))]);
        Assert.DoesNotContain(r.Factors, f => f.RuleId == "recent_fever");
    }

    [Fact]
    public void Factor_carries_message_and_resolved_sources()
    {
        var r = Run("hr_rest", StatusLevel.Yellow, "high", [R("temp", 38, H(1))]);
        var f = Assert.Single(r.Factors);
        Assert.Equal("recent_fever", f.RuleId);
        Assert.Contains("Fever raises resting heart rate", f.Message);
        var s = Assert.Single(f.Sources);
        Assert.Equal("C10", s.Id);
        Assert.StartsWith("https://pubmed", s.Url);
    }

    [Fact]
    public void Green_reading_runs_no_rules()
    {
        var r = Run("hr_rest", StatusLevel.Green, null, [R("temp", 39, H(1))]);
        Assert.Empty(r.Factors);
        Assert.False(r.Downgraded);
    }

    // ---------- Policy ----------

    [Fact]
    public void Direction_mismatch_does_not_match()
    {
        // short_sleep and high_stress explain HIGH values only.
        var r = Run("hr_rest", StatusLevel.Yellow, "low", [R("sleep_h", 4, D(1)), R("sleep_h", 4, D(2)), R("mood", 1, H(1))]);
        Assert.Empty(r.Factors);
        Assert.False(r.Downgraded);
    }

    [Fact]
    public void Several_matching_rules_never_stack()
    {
        var r = Run("hr_rest", StatusLevel.Yellow, "high",
            [R("sleep_h", 4, D(1)), R("sleep_h", 4, D(2)), R("mood", 1, H(1)), R("temp", 38, H(1))]);
        Assert.Equal(StatusLevel.Yellow, r.OriginalLevel);
        Assert.Equal(StatusLevel.Green, r.Level);     // one level, not three
        Assert.True(r.Downgraded);
        Assert.Equal(["short_sleep", "high_stress", "recent_fever"], r.DowngradedBy);
        Assert.Equal(3, r.Factors.Count);
    }

    [Fact]
    public void Stacking_is_capped_at_one_level_even_when_red_may_be_lowered()
    {
        var rules = Rules();
        rules.Policy!.DowngradeOnlyFrom = "red";
        var r = Run("hr_rest", StatusLevel.Red, "high",
            [R("sleep_h", 4, D(1)), R("sleep_h", 4, D(2)), R("mood", 1, H(1))], rules: rules);
        Assert.Equal(StatusLevel.Yellow, r.Level);   // red -> yellow, never green
    }

    [Fact]
    public void Red_keeps_its_color_but_lists_factors()
    {
        var r = Run("bp_sys", StatusLevel.Red, "high", [R("sleep_h", 4, D(1)), R("sleep_h", 4, D(2))]);
        Assert.Equal(StatusLevel.Red, r.Level);
        Assert.False(r.Downgraded);
        Assert.Empty(r.DowngradedBy);
        Assert.Contains(r.Factors, f => f.RuleId == "short_sleep");
        Assert.Contains("keeps its color", r.DowngradeBlockedReason);
    }

    [Theory]
    [InlineData("temp")]
    [InlineData("spo2")]
    public void Never_downgrade_metrics_are_never_lowered(string metric)
    {
        // The real rules do not explain temp or SpO2, so add a -1 rule that does.
        var rules = Rules();
        rules.Rules.Add(new ContextRule
        {
            Id = "test_explainer", Enabled = true, AppliesTo = [metric], Direction = "any", Message = "test",
            Source = ["team"], SeverityModifier = -1, Review = false,
            Condition = new ContextCondition { Type = "mission_day", Op = "gte", Value = 1 },
        });
        var r = Run(metric, StatusLevel.Yellow, "low", rules: rules);
        Assert.Contains(r.Factors, f => f.RuleId == "test_explainer");
        Assert.Equal(StatusLevel.Yellow, r.Level);
        Assert.False(r.Downgraded);
        Assert.Contains("never downgraded", r.DowngradeBlockedReason);
    }

    [Fact]
    public void Disabled_rule_is_skipped()
    {
        var rules = Rules();
        rules.Rules.Single(x => x.Id == "short_sleep").Enabled = false;
        var r = Run("hr_rest", StatusLevel.Yellow, "high", [R("sleep_h", 4, D(1)), R("sleep_h", 4, D(2))], rules: rules);
        Assert.Empty(r.Factors);
        Assert.Equal(StatusLevel.Yellow, r.Level);
    }

    [Fact]
    public void Adaptation_window_shows_but_never_downgrades()
    {
        var r = Run("bp_sys", StatusLevel.Yellow, "high", at: Launch.AddDays(2));
        var f = Assert.Single(r.Factors);
        Assert.Equal("adaptation_window", f.RuleId);
        Assert.Equal(0, f.SeverityModifier);
        Assert.Equal(StatusLevel.Yellow, r.Level);
        Assert.False(r.Downgraded);
        Assert.Null(r.DowngradeBlockedReason);
    }

    [Fact]
    public void Show_only_rules_never_downgrade_even_when_several_match()
    {
        var r = Run("hr_rest", StatusLevel.Yellow, "high",
            [R("exercise_min", 0, D(1)), R("exercise_min", 0, D(2))],
            [E("kp", 8, H(1)), E("solar_particle_event", 1, H(1))]);
        Assert.Equal(["missed_exercise", "space_weather_kp", "space_weather_spe"], r.Factors.Select(f => f.RuleId));
        Assert.Equal(StatusLevel.Yellow, r.Level);
        Assert.False(r.Downgraded);
    }

    [Fact]
    public void Policy_zero_blocks_every_downgrade()
    {
        var rules = Rules();
        rules.Policy!.MaxTotalDowngrade = 0;
        var r = Run("hr_rest", StatusLevel.Yellow, "high", [R("temp", 38, H(1))], rules: rules);
        Assert.False(r.Downgraded);
        Assert.Contains("max_total_downgrade is 0", r.DowngradeBlockedReason);
    }

    [Fact]
    public void Max_lookback_covers_longest_window()
    {
        Assert.Equal(TimeSpan.FromDays(3), ContextEngine.MaxLookback(Rules())); // 3 days = 72 h
    }

    [Theory]
    [InlineData(1, "lt", 2, true)]
    [InlineData(2, "lt", 2, false)]
    [InlineData(2, "lte", 2, true)]
    [InlineData(3, "gt", 2, true)]
    [InlineData(2, "gt", 2, false)]
    [InlineData(2, "gte", 2, true)]
    public void Comparison_ops(double actual, string op, double limit, bool expected)
    {
        Assert.Equal(expected, ContextEngine.Compare(actual, op, limit));
    }
}
