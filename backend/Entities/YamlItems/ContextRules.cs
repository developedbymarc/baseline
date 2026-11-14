namespace backend.Entities;

// config/context_rules.yaml (work package 2.3). Loaded strictly: unknown keys are errors.
// Nullable members let the validator report a missing required field instead of using a default.
public class ContextRulesFile
{
    public string? Version { get; set; }
    public string? Language { get; set; }
    public ContextPolicy? Policy { get; set; }
    public List<ContextRule> Rules { get; set; } = [];
    public List<ContextSource> Sources { get; set; } = [];
}

public class ContextPolicy
{
    // Several matching rules never stack: at most this many levels in total (0 or 1).
    public int? MaxTotalDowngrade { get; set; }
    // yellow = only a yellow flag can be lowered; red = yellow and red can be lowered (plain spec I4).
    public string? DowngradeOnlyFrom { get; set; }
    public List<string> NeverDowngradeMetrics { get; set; } = [];
}

public class ContextRule
{
    public string? Id { get; set; }
    public bool? Enabled { get; set; }
    public List<string> AppliesTo { get; set; } = [];
    // high | low | any
    public string? Direction { get; set; }
    public ContextCondition? Condition { get; set; }
    public string? Message { get; set; }
    public List<string> Source { get; set; } = [];
    // 0 = show only; -1 = may lower the flag by one level.
    public int? SeverityModifier { get; set; }
    public bool? Review { get; set; }
}

public class ContextCondition
{
    // metric_count | mission_day | environment | metric_recent
    public string? Type { get; set; }
    public string? Metric { get; set; }
    // lt | gt | lte | gte
    public string? Op { get; set; }
    public double? Value { get; set; }
    // metric_count: that many MISSION DAYS counting today (whole number). Each day counts once,
    // if its latest reading passes the test; true when at least min_count days pass.
    public double? WindowDays { get; set; }
    public int? MinCount { get; set; }
    // co2_mmhg | kp | solar_particle_event
    public string? Kind { get; set; }
    public double? WindowH { get; set; }
}

public class ContextSource
{
    public string? Id { get; set; }
    public string? Publisher { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public bool? Opened { get; set; }
}
