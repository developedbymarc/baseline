using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Entities;

namespace backend.Tests;

public sealed class DbContextTests : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");

    public DbContextTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private ApplicationDbContext NewDb()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static (Mission, CrewMember) Seed(ApplicationDbContext db)
    {
        var m = new Mission { Name = "t", LaunchDate = DateTimeOffset.UtcNow };
        var c = new CrewMember { MissionId = m.Id, Name = "c", Role = "r", Sex = "F", Age = 40, HeightCm = 170 };
        db.AddRange(m, c);
        db.SaveChanges();
        return (m, c);
    }

    [Fact]
    public void Locked_baseline_cannot_be_modified_or_deleted()
    {
        using var db = NewDb();
        var (_, c) = Seed(db);
        db.Baselines.Add(new Baseline { CrewMemberId = c.Id, MetricCode = "hr_rest", Mean = 60, StandardDeviation = 3, SampleSize = 14, LockedAt = DateTimeOffset.UtcNow });
        db.SaveChanges();

        var b = db.Baselines.Single();
        b.Mean = 99;
        Assert.Throws<BaselineLockedException>(() => db.SaveChanges());

        db.ChangeTracker.Clear();
        db.Baselines.Remove(db.Baselines.Single());
        Assert.Throws<BaselineLockedException>(() => db.SaveChanges());

        db.ChangeTracker.Clear();
        Assert.Equal(60, db.Baselines.Single().Mean);
    }

    [Fact]
    public void Unlocked_baseline_can_be_edited_and_then_locked()
    {
        using var db = NewDb();
        var (_, c) = Seed(db);
        db.Baselines.Add(new Baseline { CrewMemberId = c.Id, MetricCode = "hr_rest", Mean = 60, SampleSize = 3 });
        db.SaveChanges();
        var b = db.Baselines.Single();
        b.Mean = 61;
        b.LockedAt = DateTimeOffset.UtcNow;
        db.SaveChanges();
        Assert.True(db.Baselines.AsNoTracking().Single().IsLocked);
    }

    [Fact]
    public void DateTimeOffset_filters_and_ordering_work_in_sqlite()
    {
        using var db = NewDb();
        var (_, c) = Seed(db);
        var t0 = new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
            db.Measurements.Add(new Measurement { CrewMemberId = c.Id, MetricCode = "hr_rest", Value = i, RecordedAt = t0.AddHours(4 - i) });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var cutoff = t0.AddHours(2);
        var rows = db.Measurements.Where(x => x.RecordedAt >= cutoff).OrderBy(x => x.RecordedAt).ToList();
        Assert.Equal([2.0, 1.0, 0.0], rows.Select(r => r.Value));
        Assert.Equal(TimeSpan.Zero, rows[0].RecordedAt.Offset);
        Assert.Equal(cutoff, rows[0].RecordedAt);
    }
}
