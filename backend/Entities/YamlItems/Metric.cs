namespace backend.Entities;

// One entry of config/metrics.yaml. Nothing health-related is hard-coded: every number comes from the file.
public class HealthMetric
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public double PlausibleMin { get; set; }
    public double PlausibleMax { get; set; }
    public double? NormalLow { get; set; }
    public double? NormalHigh { get; set; }
    public double? WarnLow { get; set; }
    public double? WarnHigh { get; set; }
    public double? CritLow { get; set; }
    public double? CritHigh { get; set; }
    public double? ZWarn { get; set; }
    public double? ZCrit { get; set; }
    // both | low | high | none
    public string Polarity { get; set; } = "both";
    // Population SD for the small-sample shrinkage (spec section 8). Optional until added to the YAML.
    public double? SdPop { get; set; }
    public string? InstructionsMd { get; set; }
    public List<MetricSource> Sources { get; set; } = [];

    // Context inputs (mood, exercise_min) have no limits and get no status or baseline z-score.
    public bool HasStatus => Polarity != "none";
}

public class MetricSource
{
    // Metric fields this source covers, e.g. [normal_low, warn_high]. publisher "team" = our own value.
    public List<string> Fields { get; set; } = [];
    public string? Publisher { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public string? Checked { get; set; }
    // null for team entries (nothing to open).
    public bool? Opened { get; set; }
}

public class MetricsFile
{
    public string Version { get; set; } = null!;
    public List<HealthMetric> Metrics { get; set; } = [];
}
