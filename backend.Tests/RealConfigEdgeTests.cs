using backend.Configuration;
using backend.Entities;
using backend.Services.Engines;

namespace backend.Tests;

// Band-edge rule against the real metrics.yaml: a value exactly on a limit counts as inside the band.
// If these fail after a YAML edit, the limits changed: check with the content owner before "fixing" the test.
public class RealConfigEdgeTests
{
    private static readonly HealthConfig Config = HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory);

    [Theory]
    [InlineData("bp_sys", 129, StatusLevel.Green)]
    [InlineData("bp_sys", 130, StatusLevel.Yellow)]   // AHA stage 1
    [InlineData("bp_sys", 139, StatusLevel.Yellow)]
    [InlineData("bp_sys", 140, StatusLevel.Red)]      // AHA stage 2
    [InlineData("bp_dia", 79, StatusLevel.Green)]
    [InlineData("bp_dia", 80, StatusLevel.Yellow)]
    [InlineData("bp_dia", 89, StatusLevel.Yellow)]
    [InlineData("bp_dia", 90, StatusLevel.Red)]
    [InlineData("spo2", 95, StatusLevel.Green)]
    [InlineData("spo2", 94, StatusLevel.Yellow)]
    [InlineData("spo2", 93, StatusLevel.Yellow)]
    [InlineData("spo2", 92, StatusLevel.Red)]         // MedlinePlus: contact a provider at 92% or lower
    [InlineData("hr_rest", 100, StatusLevel.Green)]
    [InlineData("hr_rest", 101, StatusLevel.Yellow)]
    [InlineData("temp", 38.0, StatusLevel.Yellow)]
    [InlineData("temp", 38.1, StatusLevel.Red)]
    [InlineData("temp", 35.0, StatusLevel.Yellow)]
    [InlineData("temp", 34.9, StatusLevel.Red)]
    [InlineData("sleep_h", 7, StatusLevel.Green)]
    [InlineData("sleep_h", 5, StatusLevel.Yellow)]
    [InlineData("sleep_h", 4.9, StatusLevel.Red)]
    public void Absolute_status_at_documented_edges(string metric, double value, StatusLevel expected)
    {
        Assert.Equal(expected, StatusEngine.Absolute(Config.GetMetric(metric), value).Level);
        // Without a baseline the final status equals the absolute status.
        Assert.Equal(expected, StatusEngine.Evaluate(Config.GetMetric(metric), value, null).Level);
    }

    public static IEnumerable<object[]> StatusMetrics() =>
        HealthConfig.LoadFromDirectory(TestPaths.ConfigDirectory).Metrics.Values
            .Where(m => m.HasStatus).Select(m => new object[] { m.Code });

    [Theory]
    [MemberData(nameof(StatusMetrics))]
    public void Every_limit_value_is_inside_its_own_band(string code)
    {
        var m = Config.GetMetric(code);
        if (m.WarnLow is { } wl) Assert.Equal(StatusLevel.Green, StatusEngine.Absolute(m, wl).Level);
        if (m.WarnHigh is { } wh) Assert.Equal(StatusLevel.Green, StatusEngine.Absolute(m, wh).Level);
        if (m.CritLow is { } cl && cl < (m.WarnLow ?? double.MaxValue))
            Assert.Equal(StatusLevel.Yellow, StatusEngine.Absolute(m, cl).Level);
        if (m.CritHigh is { } ch && ch > (m.WarnHigh ?? double.MinValue))
            Assert.Equal(StatusLevel.Yellow, StatusEngine.Absolute(m, ch).Level);
    }

    [Fact]
    public void Real_sd_pop_shrinks_small_baselines()
    {
        // bp_sys sd_pop 19.34; n=6, personal sd 5: sqrt((6*25 + 10*19.34^2) / 16)
        var m = Config.GetMetric("bp_sys");
        var expected = Math.Sqrt((6 * 25 + 10 * 19.34 * 19.34) / 16);
        var r = StatusEngine.ZScore(m, 120, new BaselineSnapshot(110, 5, 6));
        Assert.Equal(expected, r.SdEff!.Value, 10);
        Assert.True(BaselineEngine.IsShrunk(6, m.SdPop));
    }

    [Fact]
    public void Null_sd_pop_falls_back_to_personal_sd()
    {
        // hr_rest has sd_pop null: n=6, personal sd 3 -> sd_eff 3, z(70 vs 64) = 2.0 = yellow
        var m = Config.GetMetric("hr_rest");
        var r = StatusEngine.ZScore(m, 70, new BaselineSnapshot(64, 3, 6));
        Assert.Equal(3, r.SdEff);
        Assert.Equal(2.0, r.Z!.Value, 10);
        Assert.Equal(StatusLevel.Yellow, r.Level);
        Assert.False(BaselineEngine.IsShrunk(6, m.SdPop));
    }
}
