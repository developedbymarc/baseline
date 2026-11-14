using System.Globalization;
using backend.Entities;
using backend.Services.Engines;

namespace backend.Services.Reports;

public sealed record ReportReading(double Value, DateTimeOffset RecordedAt);

public sealed record MetricRow(
    string MetricCode,
    string MetricName,
    string Unit,
    int WindowN,
    double? WindowMean,
    double? BaselineMean,
    int? BaselineN,
    double? SdEffective,
    bool BaselineShrunk,
    // "locked" (stored at mission start) or "preflight" (context inputs: computed from pre-flight data, not locked).
    string? BaselineSource,
    double? Delta,
    // Headline: SDs of normal variation, (window mean - baseline mean) / sd_eff (spec section 8 item 2).
    double? ZSd,
    // "Is the average shifted": (window mean - baseline mean) / (sd_eff / sqrt(window n)).
    double? ZShift,
    double? TrendSlopePerDay,
    double? TrendChangeOverWindow,
    double? TrendPValue,
    // rising | falling | stable | insufficient
    string Trend,
    int Missed,
    int Expected,
    bool LowConfidence);

// Pure report maths for one metric in one window (WP 4.4, spec section 8, stories D3/D4).
public static class ReportEngine
{
    private static string F(double v, string fmt = "0.#") => v.ToString(fmt, CultureInfo.InvariantCulture);

    public static MetricRow BuildRow(
        HealthMetric metric, IReadOnlyList<ReportReading> windowReadings, BaselineSnapshot? baseline, string? baselineSource,
        DateTimeOffset periodStart, double periodDays, int missed, int expected, ReportOptions options)
    {
        var ordered = windowReadings.OrderBy(r => r.RecordedAt).ToList();
        var n = ordered.Count;
        double? mean = n > 0 ? ordered.Average(r => r.Value) : null;

        double? sdEff = baseline is null ? null : BaselineEngine.EffectiveSd(baseline.Sd, baseline.N, metric.SdPop);
        double? delta = mean is { } m && baseline is not null ? m - baseline.Mean : null;
        double? zSd = delta is { } d && sdEff is > 0 ? d / sdEff : null;
        double? zShift = delta is { } d2 && sdEff is > 0 && n > 0 ? d2 / (sdEff.Value / Math.Sqrt(n)) : null;

        var x = ordered.Select(r => (r.RecordedAt - periodStart).TotalDays).ToList();
        var y = ordered.Select(r => r.Value).ToList();
        var slope = TrendStats.TheilSenSlope(x, y);
        var mk = TrendStats.MannKendall(y);
        double? change = slope * periodDays;

        string trend;
        if (n < 3 || slope is null) trend = "insufficient";
        else
        {
            var bigEnough = sdEff is > 0 && Math.Abs(change!.Value) >= options.TrendMinEffectSd * sdEff.Value;
            trend = mk.PValue < options.TrendMaxPValue && bigEnough
                ? (slope > 0 ? "rising" : "falling")
                : "stable";
        }

        return new MetricRow(metric.Code, metric.Name, metric.Unit, n, mean, baseline?.Mean, baseline?.N, sdEff,
            baseline is not null && BaselineEngine.IsShrunk(baseline.N, metric.SdPop), baseline is null ? null : baselineSource,
            delta, zSd, zShift, slope, change, n >= 3 ? mk.PValue : null, trend, missed, expected,
            n < options.MinPointsForConfidence);
    }

    // Same rule as the crew-side reminders: each reading completes the OLDEST open scheduled slot whose window
    // it falls in (from due - early window to the next due time); one reading never completes two slots.
    // A slot is missed when it is still open once its next due time has passed.
    public static (int Missed, int Expected) CountMissed(
        IReadOnlyList<DateTimeOffset> dueTimes, IReadOnlyList<DateTimeOffset> readingTimes, DateTimeOffset now,
        TimeSpan earlyWindow, TimeSpan interval)
    {
        var due = dueTimes.Where(t => t <= now).OrderBy(t => t).ToList();
        var until = due.Select((t, i) => i + 1 < due.Count ? due[i + 1] : t + interval).ToList();
        var done = new bool[due.Count];
        foreach (var t in readingTimes.OrderBy(t => t))
        {
            for (var i = 0; i < due.Count; i++)
            {
                if (done[i] || t < due[i] - earlyWindow || t >= until[i]) continue;
                done[i] = true;
                break;
            }
        }
        var missed = Enumerable.Range(0, due.Count).Count(i => !done[i] && until[i] <= now);
        return (missed, due.Count);
    }

    // Plain-language highlights from fixed templates (D4): deterministic, no LLM.
    // A shift is mentioned when |z| reaches the metric's own z_warn from metrics.yaml.
    public static List<string> Highlights(IEnumerable<MetricRow> rows, Func<string, double?> zWarnFor)
    {
        var list = new List<string>();
        foreach (var r in rows)
        {
            var zWarn = zWarnFor(r.MetricCode);
            var shift = r.ZSd is { } z && zWarn is { } zw && Math.Abs(z) >= zw
                ? $"now {(z >= 0 ? "+" : "")}{F(z)} SD {(z >= 0 ? "above" : "below")} baseline (n={r.BaselineN})"
                : null;
            if (r.Trend is "rising" or "falling")
            {
                var verb = r.Trend == "rising" ? "rose" : "fell";
                var perDay = $"{(r.TrendSlopePerDay >= 0 ? "+" : "")}{F(r.TrendSlopePerDay!.Value, "0.##")} {r.Unit}/day";
                var pText = r.TrendPValue < 0.001 ? "p < 0.001" : $"p = {F(r.TrendPValue!.Value, "0.###")}";
                list.Add($"{r.MetricName} {verb} over this period ({perDay}, Mann-Kendall {pText})"
                         + (shift is null ? "." : $", {shift}."));
            }
            else if (shift is not null)
                list.Add($"{r.MetricName} averaged {F(r.WindowMean!.Value)} {r.Unit}, {shift}.");

            if (r.Missed > 0)
                list.Add($"{r.MetricName}: {r.Missed} of {r.Expected} scheduled measurements missed.");
            if (r.LowConfidence && r.Expected > 0)
                list.Add($"{r.MetricName}: only {r.WindowN} readings in this period, low confidence.");
        }
        return list;
    }
}
