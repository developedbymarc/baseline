using backend.Services.Engines;

namespace backend.Tests;

public class BaselineEngineTests
{
    [Fact]
    public void Mean_and_sample_sd_match_hand_computation()
    {
        // mean = 3; squared deviations 4+1+0+1+4 = 10; sample variance 10/4 = 2.5
        var s = BaselineEngine.Compute([1, 2, 3, 4, 5])!;
        Assert.Equal(3.0, s.Mean, 10);
        Assert.Equal(Math.Sqrt(2.5), s.Sd, 10);
        Assert.Equal(5, s.N);
    }

    [Fact]
    public void Realistic_resting_hr_series()
    {
        // 62,64,66,68,70: mean 66, squared deviations 16+4+0+4+16 = 40, variance 10
        var s = BaselineEngine.Compute([62, 64, 66, 68, 70])!;
        Assert.Equal(66.0, s.Mean, 10);
        Assert.Equal(Math.Sqrt(10), s.Sd, 10);
    }

    [Fact]
    public void Single_value_has_zero_sd_and_no_values_has_no_baseline()
    {
        var one = BaselineEngine.Compute([72])!;
        Assert.Equal(72, one.Mean);
        Assert.Equal(0, one.Sd);
        Assert.Equal(1, one.N);
        Assert.Null(BaselineEngine.Compute([]));
    }

    [Fact]
    public void Shrunk_sd_for_small_samples_matches_formula()
    {
        // n=6, sd=2, sd_pop=4, k=10: sqrt((6*4 + 10*16) / 16) = sqrt(11.5)
        Assert.Equal(Math.Sqrt(11.5), BaselineEngine.EffectiveSd(2, 6, 4), 10);
        Assert.True(BaselineEngine.IsShrunk(6, 4));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(30)]
    public void No_shrinkage_from_n_10(int n)
    {
        Assert.Equal(2.0, BaselineEngine.EffectiveSd(2, n, 4));
        Assert.False(BaselineEngine.IsShrunk(n, 4));
    }

    [Fact]
    public void No_shrinkage_without_population_sd()
    {
        Assert.Equal(2.0, BaselineEngine.EffectiveSd(2, 5, null));
        Assert.False(BaselineEngine.IsShrunk(5, null));
    }

    [Fact]
    public void Shrinkage_with_zero_personal_sd_uses_population_share()
    {
        // n=1, sd=0, sd_pop=3: sqrt(10*9/11)
        Assert.Equal(Math.Sqrt(90.0 / 11), BaselineEngine.EffectiveSd(0, 1, 3), 10);
    }
}
