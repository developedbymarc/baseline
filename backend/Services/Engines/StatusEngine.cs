using System.Globalization;
using backend.Entities;

namespace backend.Services.Engines;

public sealed record BaselineSnapshot(double Mean, double Sd, int N);

public sealed record StatusResult(
    StatusLevel Level,
    StatusLevel AbsoluteLevel,
    StatusLevel ZLevel,
    double? Z,
    double? SdEffective,
    int? BaselineN,
    // "none", "threshold", "zscore" or "threshold+zscore": which rule set the final level (story C2).
    string Rule,
    string Reason,
    // "high" or "low": which side of normal set the final level (null when green). Context rules match on it.
    string? Direction = null);

public sealed record PlausibilityResult(bool Plausible, string? Message);

// WP 4.2, spec C2 / section 8 item 5: final status = worse of absolute-threshold status and z-score status.
// All limits come from config/metrics.yaml. Band edges are strict: a value equal to a limit is inside it.
public static class StatusEngine
{
    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static (StatusLevel Level, string? Reason) Absolute(HealthMetric m, double v)
    {
        if (m.CritLow is { } cl && v < cl) return (StatusLevel.Red, $"Below the critical limit of {F(cl)} {m.Unit}.");
        if (m.CritHigh is { } ch && v > ch) return (StatusLevel.Red, $"Above the critical limit of {F(ch)} {m.Unit}.");
        if (m.WarnLow is { } wl && v < wl) return (StatusLevel.Yellow, $"Below the warning limit of {F(wl)} {m.Unit}.");
        if (m.WarnHigh is { } wh && v > wh) return (StatusLevel.Yellow, $"Above the warning limit of {F(wh)} {m.Unit}.");
        return (StatusLevel.Green, null);
    }

    // z = (value - baseline mean) / sd_eff. Polarity decides which side counts:
    // low = only falling values are flagged, high = only rising, both = either, none = never.
    public static (StatusLevel Level, double? Z, double? SdEff, string? Reason) ZScore(
        HealthMetric m, double v, BaselineSnapshot? b)
    {
        if (b is null || m.ZWarn is not { } zw || m.ZCrit is not { } zc || m.Polarity == "none")
            return (StatusLevel.Green, null, null, null);

        var sdEff = BaselineEngine.EffectiveSd(b.Sd, b.N, m.SdPop);
        if (sdEff <= 0) return (StatusLevel.Green, null, sdEff, null);

        var z = (v - b.Mean) / sdEff;
        var flagged = m.Polarity switch
        {
            "low" => -z,
            "high" => z,
            _ => Math.Abs(z),
        };

        var level = flagged > zc ? StatusLevel.Red : flagged > zw ? StatusLevel.Yellow : StatusLevel.Green;
        var sign = z >= 0 ? "+" : "";
        var reason = level == StatusLevel.Green
            ? null
            : $"{sign}{z.ToString("0.0", CultureInfo.InvariantCulture)} SD from your personal baseline (n={b.N}).";
        return (level, z, sdEff, reason);
    }

    public static StatusResult Evaluate(HealthMetric m, double v, BaselineSnapshot? b)
    {
        if (!m.HasStatus)
            return new StatusResult(StatusLevel.Green, StatusLevel.Green, StatusLevel.Green, null, null, null,
                "none", "Logged. This is a context input with no status limits.");

        var (abs, absReason) = Absolute(m, v);
        var (zl, z, sdEff, zReason) = ZScore(m, v, b);
        var level = (StatusLevel)Math.Max((int)abs, (int)zl);
        var absDirection = abs == StatusLevel.Green ? null
            : (m.WarnHigh is { } wh && v > wh) || (m.CritHigh is { } ch && v > ch) ? "high" : "low";
        var zDirection = zl == StatusLevel.Green || z is null ? null : z > 0 ? "high" : "low";

        string rule;
        string reason;
        if (level == StatusLevel.Green)
        {
            rule = "none";
            reason = b is null
                ? "Within limits. No personal baseline yet."
                : $"Within limits and within {F(m.ZWarn!.Value)} SD of your baseline (n={b.N}).";
        }
        else if (abs == zl)
        {
            rule = "threshold+zscore";
            reason = $"{absReason} {zReason}";
        }
        else if (abs > zl)
        {
            rule = "threshold";
            reason = absReason!;
        }
        else
        {
            rule = "zscore";
            reason = zReason!;
        }

        // When both rules fire, the absolute limit decides the direction.
        var direction = rule switch
        {
            "zscore" => zDirection,
            "none" => null,
            _ => absDirection,
        };
        return new StatusResult(level, abs, zl, z, sdEff, b?.N, rule, reason, direction);
    }

    // Story C3: catch typos and wrong units before saving.
    public static PlausibilityResult CheckPlausible(HealthMetric m, double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return new(false, "Not a number.");
        if (v >= m.PlausibleMin && v <= m.PlausibleMax) return new(true, null);

        var msg = $"{F(v)} {m.Unit} is outside the plausible range {F(m.PlausibleMin)}-{F(m.PlausibleMax)} {m.Unit}. Check the unit and retake, or confirm.";
        if (m.Unit == "°C")
        {
            var c = (v - 32) * 5 / 9;
            if (c >= m.PlausibleMin && c <= m.PlausibleMax)
                msg = $"{F(v)} looks like Fahrenheit ({F(v)} °F = {c.ToString("0.0", CultureInfo.InvariantCulture)} °C). This field is in °C.";
        }
        return new(false, msg);
    }
}
