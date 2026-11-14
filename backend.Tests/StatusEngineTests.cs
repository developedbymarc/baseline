using backend.Entities;
using backend.Services.Engines;

namespace backend.Tests;

public class StatusEngineTests
{
    // Limits: crit 40 / warn 50 .. warn 100 / crit 120. Edges are strict (equal to a limit = inside it).
    [Theory]
    [InlineData(70, StatusLevel.Green)]
    [InlineData(50, StatusLevel.Green)]
    [InlineData(100, StatusLevel.Green)]
    [InlineData(100.1, StatusLevel.Yellow)]
    [InlineData(49.9, StatusLevel.Yellow)]
    [InlineData(120, StatusLevel.Yellow)]
    [InlineData(40, StatusLevel.Yellow)]
    [InlineData(120.1, StatusLevel.Red)]
    [InlineData(39.9, StatusLevel.Red)]
    public void Absolute_bands(double value, StatusLevel expected)
    {
        Assert.Equal(expected, StatusEngine.Absolute(TestMetrics.HeartRate(), value).Level);
    }

    [Fact]
    public void Null_limits_are_skipped()
    {
        var m = TestMetrics.HeartRate();
        m.WarnHigh = null;
        m.CritHigh = null;
        Assert.Equal(StatusLevel.Green, StatusEngine.Absolute(m, 200).Level);
        Assert.Equal(StatusLevel.Red, StatusEngine.Absolute(m, 30).Level);
    }

    private static readonly BaselineSnapshot Base = new(Mean: 70, Sd: 4, N: 14);

    // z = (v - 70) / 4 ; yellow above 1.5, red above 2.5
    [Theory]
    [InlineData(76, StatusLevel.Green, 1.5)]   // exactly 1.5 is not above it
    [InlineData(77, StatusLevel.Yellow, 1.75)]
    [InlineData(80, StatusLevel.Yellow, 2.5)]  // exactly 2.5 is still yellow
    [InlineData(81, StatusLevel.Red, 2.75)]
    [InlineData(59, StatusLevel.Red, -2.75)]
    public void ZScore_bands_for_polarity_both(double value, StatusLevel expected, double z)
    {
        var r = StatusEngine.ZScore(TestMetrics.HeartRate(), value, Base);
        Assert.Equal(expected, r.Level);
        Assert.Equal(z, r.Z!.Value, 10);
    }

    [Fact]
    public void Polarity_low_ignores_rising_values()
    {
        var m = TestMetrics.HeartRate("low");
        Assert.Equal(StatusLevel.Green, StatusEngine.ZScore(m, 90, Base).Level);
        Assert.Equal(StatusLevel.Red, StatusEngine.ZScore(m, 55, Base).Level);
    }

    [Fact]
    public void Polarity_high_ignores_falling_values()
    {
        var m = TestMetrics.HeartRate("high");
        Assert.Equal(StatusLevel.Green, StatusEngine.ZScore(m, 55, Base).Level);
        Assert.Equal(StatusLevel.Red, StatusEngine.ZScore(m, 85, Base).Level);
    }

    [Fact]
    public void Shrinkage_widens_sd_for_small_n()
    {
        // n=5, sd=2, sd_pop=6: sd_eff = sqrt((5*4 + 10*36)/15) = sqrt(25.333) = 5.033; z(80) = 10/5.033 = 1.99
        var m = TestMetrics.HeartRate(sdPop: 6);
        var r = StatusEngine.ZScore(m, 80, new BaselineSnapshot(70, 2, 5));
        Assert.Equal(Math.Sqrt(380.0 / 15), r.SdEff!.Value, 10);
        Assert.Equal(StatusLevel.Yellow, r.Level); // unshrunk z would be 5.0 = red
    }

    [Fact]
    public void Zero_sd_gives_no_z_score()
    {
        var r = StatusEngine.ZScore(TestMetrics.HeartRate(), 90, new BaselineSnapshot(70, 0, 14));
        Assert.Null(r.Z);
        Assert.Equal(StatusLevel.Green, r.Level);
    }

    // Final = worse of absolute and z; rule says which fired.
    [Theory]
    [InlineData(72, StatusLevel.Green, "none")]
    [InlineData(78, StatusLevel.Yellow, "zscore")]            // abs green, z 2.0 yellow
    [InlineData(82, StatusLevel.Red, "zscore")]               // abs green, z 3.0 red
    [InlineData(101, StatusLevel.Red, "zscore")]              // abs yellow, z 7.75 red
    [InlineData(125, StatusLevel.Red, "threshold+zscore")]    // both red
    public void Final_status_is_worse_of_both(double value, StatusLevel level, string rule)
    {
        var r = StatusEngine.Evaluate(TestMetrics.HeartRate(), value, Base);
        Assert.Equal(level, r.Level);
        Assert.Equal(rule, r.Rule);
        Assert.False(string.IsNullOrWhiteSpace(r.Reason));
    }

    [Fact]
    public void Threshold_rule_fires_when_z_is_quiet()
    {
        // Trained athlete: baseline 45 bpm, so 45 is z 0 but below the warning limit 50.
        var r = StatusEngine.Evaluate(TestMetrics.HeartRate(), 45, new BaselineSnapshot(45, 3, 14));
        Assert.Equal(StatusLevel.Yellow, r.Level);
        Assert.Equal("threshold", r.Rule);
        Assert.Contains("warning limit", r.Reason);
    }

    [Fact]
    public void Without_baseline_only_thresholds_apply()
    {
        var r = StatusEngine.Evaluate(TestMetrics.HeartRate(), 85, null);
        Assert.Equal(StatusLevel.Green, r.Level);
        Assert.Null(r.Z);
        Assert.Null(r.BaselineN);
    }

    [Fact]
    public void Reason_states_sd_and_n()
    {
        var r = StatusEngine.Evaluate(TestMetrics.HeartRate(), 78.4, Base);
        Assert.Contains("+2.1 SD", r.Reason);
        Assert.Contains("n=14", r.Reason);
    }

    [Fact]
    public void Context_inputs_have_no_status()
    {
        var r = StatusEngine.Evaluate(TestMetrics.Context(), 1, null);
        Assert.Equal(StatusLevel.Green, r.Level);
        Assert.Equal("none", r.Rule);
    }

    [Theory]
    [InlineData(37.0, true)]
    [InlineData(30, true)]
    [InlineData(43, true)]
    [InlineData(29.9, false)]
    [InlineData(98.6, false)]
    public void Plausibility_range(double value, bool plausible)
    {
        Assert.Equal(plausible, StatusEngine.CheckPlausible(TestMetrics.Temperature(), value).Plausible);
    }

    [Fact]
    public void Fahrenheit_in_celsius_field_is_explained()
    {
        var r = StatusEngine.CheckPlausible(TestMetrics.Temperature(), 98.6);
        Assert.Contains("Fahrenheit", r.Message);
        Assert.Contains("37.0 °C", r.Message);
    }

    [Fact]
    public void Nan_is_implausible()
    {
        Assert.False(StatusEngine.CheckPlausible(TestMetrics.HeartRate(), double.NaN).Plausible);
    }

    // Direction feeds the context rules (high / low / any).
    [Theory]
    [InlineData(101, "high")]    // above warn_high
    [InlineData(45, "low")]      // below warn_low (baseline 70: z also low)
    [InlineData(78, "high")]     // z-score only
    [InlineData(62, "low")]      // z = -2.0
    [InlineData(72, null)]       // green
    public void Direction_of_deviation(double value, string? expected)
    {
        Assert.Equal(expected, StatusEngine.Evaluate(TestMetrics.HeartRate(), value, Base).Direction);
    }

    [Fact]
    public void Absolute_limit_decides_direction_when_both_rules_fire()
    {
        // Athlete baseline 30 bpm: 125 is red by threshold (high) and by z (high).
        Assert.Equal("high", StatusEngine.Evaluate(TestMetrics.HeartRate(), 125, new BaselineSnapshot(30, 3, 14)).Direction);
    }
}
