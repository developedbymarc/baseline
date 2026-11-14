namespace backend.Services.Reports;

public sealed record MannKendallResult(int N, double S, double Variance, double Z, double PValue);

// Spec section 8 item 3: Theil-Sen slope with the Mann-Kendall test (robust to outliers and missing data).
public static class TrendStats
{
    // Median of all pairwise slopes (y_j - y_i) / (x_j - x_i) for x_j != x_i. Null with fewer than 2 distinct x.
    public static double? TheilSenSlope(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count) throw new ArgumentException("x and y must have the same length.");
        var slopes = new List<double>();
        for (var i = 0; i < x.Count; i++)
            for (var j = i + 1; j < x.Count; j++)
                if (x[j] != x[i]) slopes.Add((y[j] - y[i]) / (x[j] - x[i]));
        if (slopes.Count == 0) return null;
        slopes.Sort();
        var mid = slopes.Count / 2;
        return slopes.Count % 2 == 1 ? slopes[mid] : (slopes[mid - 1] + slopes[mid]) / 2;
    }

    // Two-sided Mann-Kendall test on y in time order, with the tie correction and continuity correction.
    public static MannKendallResult MannKendall(IReadOnlyList<double> y)
    {
        var n = y.Count;
        if (n < 3) return new MannKendallResult(n, 0, 0, 0, 1);

        double s = 0;
        for (var i = 0; i < n; i++)
            for (var j = i + 1; j < n; j++)
                s += Math.Sign(y[j] - y[i]);

        var tieTerm = y.GroupBy(v => v).Select(g => g.Count()).Where(t => t > 1).Sum(t => (double)t * (t - 1) * (2 * t + 5));
        var variance = ((double)n * (n - 1) * (2 * n + 5) - tieTerm) / 18.0;
        if (variance <= 0) return new MannKendallResult(n, s, variance, 0, 1);

        var z = s > 0 ? (s - 1) / Math.Sqrt(variance) : s < 0 ? (s + 1) / Math.Sqrt(variance) : 0;
        var p = 2 * (1 - NormalCdf(Math.Abs(z)));
        return new MannKendallResult(n, s, variance, z, Math.Clamp(p, 0, 1));
    }

    public static double NormalCdf(double z) => 0.5 * Erfc(-z / Math.Sqrt(2));

    // Complementary error function (Numerical Recipes erfcc, fractional error < 1.2e-7).
    public static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var r = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
                    t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
                    t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0 ? r : 2.0 - r;
    }
}
