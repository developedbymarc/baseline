using backend.Entities;

namespace backend.Services.Engines;

public sealed record ContextReading(string MetricCode, double Value, DateTimeOffset RecordedAt);
public sealed record ContextEnvironmentEntry(string Kind, double Value, DateTimeOffset RecordedAt);

public sealed record ContextFactorSource(string Id, string? Publisher, string? Title, string? Url);

public sealed record ContextFactor(
    string RuleId, string Message, int SeverityModifier, IReadOnlyList<ContextFactorSource> Sources);

public sealed record ContextResult(
    // Level from the status engine. Never hidden: it is logged and shown even when downgraded.
    StatusLevel OriginalLevel,
    // Level after the policy (at most one level lower than OriginalLevel).
    StatusLevel Level,
    bool Downgraded,
    // Rules with severity_modifier -1 that caused the downgrade.
    IReadOnlyList<string> DowngradedBy,
    // Why matching -1 rules did not lower the flag (red, never_downgrade metric, policy 0), or null.
    string? DowngradeBlockedReason,
    IReadOnlyList<ContextFactor> Factors);

// Work package 4.3, stories I1-I4. Pure: the caller passes the readings and environment entries.
// Every window counts back from the reading's MISSION time and includes both ends; readings after it are ignored.
// metric_count works in whole MISSION DAYS; metric_recent and environment work in hours.
public static class ContextEngine
{
    // Mission day 1 = the first 24 h after launch.
    public static int MissionDay(DateTimeOffset launch, DateTimeOffset at) =>
        (int)Math.Floor((at - launch).TotalDays) + 1;

    public static bool Compare(double actual, string op, double limit) => op switch
    {
        "lt" => actual < limit,
        "gt" => actual > limit,
        "lte" => actual <= limit,
        "gte" => actual >= limit,
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "op must be lt, gt, lte or gte"),
    };

    // Longest look-back any enabled rule needs, so the caller can load just enough data.
    // window_days mission days counting today start less than window_days x 24 h before any time today.
    public static TimeSpan MaxLookback(ContextRulesFile rules) =>
        rules.Rules.Where(r => r.Enabled == true && r.Condition is not null)
            .Select(r => r.Condition!.WindowDays is { } d ? TimeSpan.FromDays(d)
                : r.Condition.WindowH is { } h ? TimeSpan.FromHours(h) : TimeSpan.Zero)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();

    public static ContextResult Evaluate(
        ContextRulesFile rules,
        string metricCode,
        StatusLevel level,
        string? direction,
        DateTimeOffset at,
        DateTimeOffset launch,
        IReadOnlyCollection<ContextReading> readings,
        IReadOnlyCollection<ContextEnvironmentEntry> environment)
    {
        if (level == StatusLevel.Green)
            return new ContextResult(level, level, false, [], null, []);

        var sources = rules.Sources.Where(s => s.Id is not null).ToDictionary(s => s.Id!);
        var matched = new List<ContextRule>();

        foreach (var rule in rules.Rules)
        {
            if (rule.Enabled != true || rule.Condition is null) continue;
            if (!rule.AppliesTo.Contains(metricCode)) continue;
            if (rule.Direction != "any" && rule.Direction != direction) continue;
            if (ConditionHolds(rule.Condition, at, launch, readings, environment)) matched.Add(rule);
        }

        var factors = matched.Select(r => new ContextFactor(
            r.Id!, r.Message!, r.SeverityModifier ?? 0,
            r.Source.Select(id => sources.TryGetValue(id, out var s)
                ? new ContextFactorSource(id, s.Publisher, s.Title, s.Url)
                : new ContextFactorSource(id, id == "team" ? "team" : null, null, null)).ToList())).ToList();

        // Policy: only severity_modifier -1 rules can lower a flag; never stack beyond max_total_downgrade.
        var lowering = matched.Where(r => r.SeverityModifier == -1).Select(r => r.Id!).ToList();
        var policy = rules.Policy;
        string? blocked = null;
        var downgrade = false;

        if (lowering.Count > 0)
        {
            var allowedFrom = policy?.DowngradeOnlyFrom == "red"
                ? new[] { StatusLevel.Yellow, StatusLevel.Red }
                : new[] { StatusLevel.Yellow };

            if (policy?.MaxTotalDowngrade is not > 0)
                blocked = "Policy allows no downgrade (max_total_downgrade is 0).";
            else if (policy.NeverDowngradeMetrics.Contains(metricCode))
                blocked = $"{metricCode} is never downgraded (policy never_downgrade_metrics).";
            else if (!allowedFrom.Contains(level))
                blocked = $"A {level.ToString().ToLowerInvariant()} flag keeps its color (policy downgrade_only_from: {policy.DowngradeOnlyFrom}).";
            else
                downgrade = true;
        }

        // At most one level in total, however many rules matched.
        var finalLevel = downgrade ? (StatusLevel)((int)level - 1) : level;
        return new ContextResult(level, finalLevel, downgrade, downgrade ? lowering : [], blocked, factors);
    }

    private static bool ConditionHolds(
        ContextCondition c, DateTimeOffset at, DateTimeOffset launch,
        IReadOnlyCollection<ContextReading> readings, IReadOnlyCollection<ContextEnvironmentEntry> environment)
    {
        bool InWindow(DateTimeOffset t, TimeSpan window) => t <= at && t >= at - window;

        switch (c.Type)
        {
            case "metric_count":
            {
                // Distinct mission days, not readings: the last window_days mission days counting today.
                // Each day counts once, if its latest reading (up to now) passes the test.
                var today = MissionDay(launch, at);
                var firstDay = today - (int)c.WindowDays!.Value + 1;
                var passingDays = readings
                    .Where(r => r.MetricCode == c.Metric && r.RecordedAt <= at)
                    .GroupBy(r => MissionDay(launch, r.RecordedAt))
                    .Where(g => g.Key >= firstDay && g.Key <= today)
                    .Count(g => Compare(g.MaxBy(r => r.RecordedAt)!.Value, c.Op!, c.Value!.Value));
                return passingDays >= c.MinCount!.Value;
            }
            case "metric_recent":
            {
                var window = TimeSpan.FromHours(c.WindowH!.Value);
                return readings.Any(r => r.MetricCode == c.Metric && InWindow(r.RecordedAt, window)
                                         && Compare(r.Value, c.Op!, c.Value!.Value));
            }
            case "mission_day":
                return Compare(MissionDay(launch, at), c.Op!, c.Value!.Value);
            case "environment":
            {
                var window = TimeSpan.FromHours(c.WindowH!.Value);
                return environment.Any(e => e.Kind == c.Kind && InWindow(e.RecordedAt, window)
                                            && (c.Kind == "solar_particle_event" || Compare(e.Value, c.Op!, c.Value!.Value)));
            }
            default:
                return false;
        }
    }
}
