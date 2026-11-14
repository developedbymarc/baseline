namespace backend.Services.Engines;

public sealed record BaselineStats(double Mean, double Sd, int N);

// WP 4.1, spec section 8 item 1.
public static class BaselineEngine
{
    // Spec section 8: shrink toward the population SD when n < 10, with k about 10.
    public const int ShrinkBelowN = 10;
    public const double ShrinkageK = 10;

    // Mean and sample SD (n - 1). One value gives SD 0; no values gives null.
    public static BaselineStats? Compute(IReadOnlyCollection<double> values)
    {
        var n = values.Count;
        if (n == 0) return null;
        var mean = values.Average();
        if (n == 1) return new BaselineStats(mean, 0, 1);
        var ss = values.Sum(v => (v - mean) * (v - mean));
        return new BaselineStats(mean, Math.Sqrt(ss / (n - 1)), n);
    }

    // sd_eff = sqrt((n*sd^2 + k*sd_pop^2) / (n+k)) when n < 10 and sd_pop is known; otherwise the personal SD.
    public static double EffectiveSd(double sd, int n, double? sdPop)
    {
        if (n >= ShrinkBelowN || sdPop is not { } pop) return sd;
        return Math.Sqrt((n * sd * sd + ShrinkageK * pop * pop) / (n + ShrinkageK));
    }

    public static bool IsShrunk(int n, double? sdPop) => n < ShrinkBelowN && sdPop is not null;
}
