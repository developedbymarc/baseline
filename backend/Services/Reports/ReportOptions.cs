namespace backend.Services.Reports;

// appsettings.json "Reports". Statistics settings, not health thresholds (those stay in config/*.yaml).
public sealed class ReportOptions
{
    // Rising / falling only when the Mann-Kendall p-value is below this ...
    public double TrendMaxPValue { get; set; } = 0.05;
    // ... and the Theil-Sen change over the whole window is at least this many baseline SDs (sd_eff).
    public double TrendMinEffectSd { get; set; } = 0.5;
    // Fewer in-flight readings than this in a window -> low-confidence flag.
    public int MinPointsForConfidence { get; set; } = 5;
    // Used only if config/context_rules.yaml has no adaptation_window rule.
    public int AdaptationWindowDaysFallback { get; set; } = 14;
}
