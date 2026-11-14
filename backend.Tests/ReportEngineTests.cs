using backend.Entities;
using backend.Services.Engines;
using backend.Services.Reports;

namespace backend.Tests;

// Hand-computed cases for the report maths (WP 4.4). Reference p-values from Python math.erfc.
public class ReportEngineTests
{
    private static readonly ReportOptions Options = new();
    private static readonly DateTimeOffset Start = new(2026, 11, 28, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Theil_Sen_is_the_median_pairwise_slope()
    {
        // (0,1) (1,3) (2,2) (3,5): slopes 2, 0.5, 4/3, -1, 1, 3 -> sorted -1, 0.5, 1, 4/3, 2, 3 -> median (1 + 4/3) / 2
        Assert.Equal(7.0 / 6.0, TrendStats.TheilSenSlope([0, 1, 2, 3], [1, 3, 2, 5])!.Value, 12);
        Assert.Equal(2.0, TrendStats.TheilSenSlope([0, 1, 2], [0, 2, 4])!.Value, 12);
        Assert.Null(TrendStats.TheilSenSlope([1, 1], [3, 4])); // no distinct x
    }

    [Fact]
    public void Theil_Sen_ignores_an_outlier()
    {
        // Perfect slope 1 with one wild point: still 1.
        Assert.Equal(1.0, TrendStats.TheilSenSlope([0, 1, 2, 3, 4], [0, 1, 50, 3, 4])!.Value, 12);
    }

    [Fact]
    public void Mann_Kendall_small_example()
    {
        // [1,3,2,5]: S = 4, Var = 4*3*13/18 = 26/3, Z = (4-1)/sqrt(26/3) = 1.019049, p = 0.308180
        var r = TrendStats.MannKendall([1, 3, 2, 5]);
        Assert.Equal(4, r.S);
        Assert.Equal(26.0 / 3.0, r.Variance, 10);
        Assert.Equal(1.0190493307, r.Z, 8);
        Assert.Equal(0.3081795475, r.PValue, 6);
    }

    [Fact]
    public void Mann_Kendall_strictly_increasing()
    {
        // n = 10: S = 45, Var = 10*9*25/18 = 125, Z = 44/sqrt(125) = 3.935480, p = 8.30307e-5
        var r = TrendStats.MannKendall(Enumerable.Range(1, 10).Select(i => (double)i).ToList());
        Assert.Equal(45, r.S);
        Assert.Equal(125, r.Variance, 10);
        Assert.Equal(3.9354796404, r.Z, 8);
        Assert.Equal(8.303070e-5, r.PValue, 8);
    }

    [Fact]
    public void Mann_Kendall_with_ties()
    {
        // [1,1,2]: S = 2, Var = (3*2*11 - 2*1*9)/18 = 48/18, Z = 1/sqrt(48/18) = 0.612372, p = 0.540291
        var r = TrendStats.MannKendall([1, 1, 2]);
        Assert.Equal(2, r.S);
        Assert.Equal(48.0 / 18.0, r.Variance, 10);
        Assert.Equal(0.5402913746, r.PValue, 6);
    }

    [Fact]
    public void Mann_Kendall_degenerate_cases()
    {
        Assert.Equal(1, TrendStats.MannKendall([1, 2]).PValue);       // n < 3
        Assert.Equal(1, TrendStats.MannKendall([5, 5, 5, 5]).PValue); // all ties: variance 0
        // Mirror of [1,3,2,5] (S = -4): same two-sided p.
        Assert.Equal(0.3081795475, TrendStats.MannKendall([5, 2, 3, 1]).PValue, 6);
    }

    [Fact]
    public void Normal_cdf_matches_tables()
    {
        Assert.Equal(0.9750021049, TrendStats.NormalCdf(1.96), 6);
        Assert.Equal(0.5, TrendStats.NormalCdf(0), 7);
    }

    private static HealthMetric Bp() => new()
    {
        Code = "bp_sys", Name = "Systolic blood pressure", Unit = "mmHg", PlausibleMin = 50, PlausibleMax = 260,
        WarnLow = 90, WarnHigh = 129, CritLow = 80, CritHigh = 139, ZWarn = 1.5, ZCrit = 2.5, Polarity = "both",
    };

    private static List<ReportReading> Daily(Func<int, double> value, int days = 14) =>
        Enumerable.Range(0, days).Select(d => new ReportReading(value(d), Start.AddDays(d).AddHours(2))).ToList();

    private static readonly BaselineSnapshot Base = new(120, 5, 14);

    [Fact]
    public void Row_window_mean_delta_and_both_z_forms()
    {
        // 4 readings of 125 vs baseline 120 (SD 5, n 14): delta 5, zSd = 5/5 = 1.0, zShift = 5 / (5/sqrt(4)) = 2.0
        var readings = Enumerable.Range(1, 4).Select(d => new ReportReading(125, Start.AddDays(d))).ToList();
        var row = ReportEngine.BuildRow(Bp(), readings, Base, "locked", Start, 14, 0, 4, Options);
        Assert.Equal(4, row.WindowN);
        Assert.Equal(125, row.WindowMean);
        Assert.Equal(120, row.BaselineMean);
        Assert.Equal(14, row.BaselineN);
        Assert.Equal(5, row.Delta);
        Assert.Equal(1.0, row.ZSd!.Value, 12);
        Assert.Equal(2.0, row.ZShift!.Value, 12);
        Assert.Equal("stable", row.Trend);   // flat: slope 0, all ties
        Assert.True(row.LowConfidence);      // 4 < 5 readings
    }

    [Fact]
    public void Rising_needs_significance_and_effect_size()
    {
        // +1 mmHg/day for 14 days: change 14 >= 0.5 * 5 -> rising.
        var rising = ReportEngine.BuildRow(Bp(), Daily(d => 120 + d), Base, "locked", Start, 14, 0, 14, Options);
        Assert.Equal("rising", rising.Trend);
        Assert.Equal(1.0, rising.TrendSlopePerDay!.Value, 9);
        Assert.Equal(14.0, rising.TrendChangeOverWindow!.Value, 9);
        Assert.True(rising.TrendPValue < 0.001);
        Assert.False(rising.LowConfidence);

        // +0.1/day: significant (monotone) but the change over 14 days is 1.4 < 2.5 -> stable.
        var tiny = ReportEngine.BuildRow(Bp(), Daily(d => 120 + 0.1 * d), Base, "locked", Start, 14, 0, 14, Options);
        Assert.True(tiny.TrendPValue < 0.05);
        Assert.Equal("stable", tiny.Trend);

        // Falling.
        var falling = ReportEngine.BuildRow(Bp(), Daily(d => 130 - d), Base, "locked", Start, 14, 0, 14, Options);
        Assert.Equal("falling", falling.Trend);

        // Big but noisy (zig-zag): not significant -> stable.
        var noisy = ReportEngine.BuildRow(Bp(), Daily(d => d % 2 == 0 ? 110 : 130), Base, "locked", Start, 14, 0, 14, Options);
        Assert.Equal("stable", noisy.Trend);
    }

    [Fact]
    public void Too_few_points_and_missing_baseline()
    {
        var two = ReportEngine.BuildRow(Bp(), Daily(d => 120 + d, 2), Base, "locked", Start, 14, 0, 14, Options);
        Assert.Equal("insufficient", two.Trend);
        Assert.Null(two.TrendPValue);

        var none = ReportEngine.BuildRow(Bp(), [], Base, "locked", Start, 14, 14, 14, Options);
        Assert.Null(none.WindowMean);
        Assert.Null(none.ZSd);
        Assert.Equal("insufficient", none.Trend);
        Assert.True(none.LowConfidence);

        var noBaseline = ReportEngine.BuildRow(Bp(), Daily(d => 120 + d), null, null, Start, 14, 0, 14, Options);
        Assert.Null(noBaseline.ZSd);
        Assert.Null(noBaseline.BaselineSource);
        Assert.Equal("stable", noBaseline.Trend); // no SD to judge the effect size against
    }

    [Fact]
    public void Shrunk_sd_is_used_for_small_baselines()
    {
        var m = Bp();
        m.SdPop = 19.34;
        // n = 6, sd 5: sd_eff = sqrt((6*25 + 10*19.34^2)/16)
        var row = ReportEngine.BuildRow(m, Daily(_ => 130, 5), new BaselineSnapshot(120, 5, 6), "locked", Start, 14, 0, 5, Options);
        var sdEff = Math.Sqrt((6 * 25 + 10 * 19.34 * 19.34) / 16);
        Assert.Equal(sdEff, row.SdEffective!.Value, 10);
        Assert.True(row.BaselineShrunk);
        Assert.Equal(10 / sdEff, row.ZSd!.Value, 10);
    }

    [Fact]
    public void Missed_measurements_from_the_schedule()
    {
        // Daily 08:00 on days 0..4; readings on days 0, 1, 3 (day 3 two hours early), now = day 4 09:00.
        var due = Enumerable.Range(0, 5).Select(d => Start.AddDays(d).AddHours(2)).ToList();       // 08:00 UTC
        var readings = new[] { Start.AddHours(2.5), Start.AddDays(1).AddHours(3), Start.AddDays(3) }; // day 3 at 06:00 = 2 h early
        var now = Start.AddDays(4).AddHours(3);
        var (missed, expected) = ReportEngine.CountMissed(due, readings, now, TimeSpan.FromHours(2), TimeSpan.FromDays(1));
        Assert.Equal(5, expected); // all five are due by now
        // The day-3 reading (06:00, two hours early) completes day 3, the latest slot whose window holds it,
        // even though day 2 is still open; day 2 is missed. Day 4 is open until day 5 08:00.
        Assert.Equal(1, missed);
    }

    [Fact]
    public void Early_reading_after_a_gap_does_not_shift_later_slots()
    {
        // Daily 08:00 slots on days 0..9; nothing on days 3-5; readings every other day at 07:55 (5 min early).
        var due = Enumerable.Range(0, 10).Select(d => Start.AddDays(d).AddHours(2)).ToList();
        var readings = Enumerable.Range(0, 10).Where(d => d is < 3 or > 5).Select(d => Start.AddDays(d).AddHours(1.9)).ToList();
        var (missed, _) = ReportEngine.CountMissed(due, readings, Start.AddDays(11), TimeSpan.FromHours(2), TimeSpan.FromDays(1));
        Assert.Equal(3, missed); // exactly the gap
    }

    [Fact]
    public void One_reading_never_completes_two_slots()
    {
        // Two slots 1 h apart, one reading inside both windows: one done, one missed.
        var due = new[] { Start, Start.AddHours(1) };
        var (missed, _) = ReportEngine.CountMissed(due, [Start.AddMinutes(30)], Start.AddDays(2), TimeSpan.FromHours(2), TimeSpan.FromDays(1));
        Assert.Equal(1, missed);
    }

    [Fact]
    public void Reading_after_the_next_due_does_not_rescue_a_missed_one()
    {
        var due = new[] { Start, Start.AddDays(1) };
        var late = new[] { Start.AddDays(1).AddHours(1) }; // completes day 1, not day 0
        var (missed, _) = ReportEngine.CountMissed(due, late, Start.AddDays(3), TimeSpan.FromHours(2), TimeSpan.FromDays(1));
        Assert.Equal(1, missed);
    }

    [Fact]
    public void Highlights_are_deterministic_templates()
    {
        var rising = ReportEngine.BuildRow(Bp(), Daily(d => 120 + d), Base, "locked", Start, 14, 2, 14, Options);
        var lines = ReportEngine.Highlights([rising], _ => 1.5);
        // window mean 126.5 -> zSd = 6.5/5 = 1.3 < 1.5, so no shift clause
        Assert.Equal("Systolic blood pressure rose over this period (+1 mmHg/day, Mann-Kendall p < 0.001).", lines[0]);
        Assert.Equal("Systolic blood pressure: 2 of 14 scheduled measurements missed.", lines[1]);
        Assert.Equal(lines, ReportEngine.Highlights([rising], _ => 1.5));

        var shifted = ReportEngine.BuildRow(Bp(), Daily(d => 125 + d), Base, "locked", Start, 14, 0, 14, Options);
        // mean 131.5 -> zSd = 11.5/5 = 2.3
        Assert.Equal("Systolic blood pressure rose over this period (+1 mmHg/day, Mann-Kendall p < 0.001), now +2.3 SD above baseline (n=14).",
            ReportEngine.Highlights([shifted], _ => 1.5)[0]);

        var flatHigh = ReportEngine.BuildRow(Bp(), Daily(_ => 130), Base, "locked", Start, 14, 0, 14, Options);
        Assert.Equal("Systolic blood pressure averaged 130 mmHg, now +2 SD above baseline (n=14).",
            ReportEngine.Highlights([flatHigh], _ => 1.5)[0]);

        Assert.Empty(ReportEngine.Highlights([flatHigh], _ => null)); // no z_warn (context input): no shift line
    }
}
