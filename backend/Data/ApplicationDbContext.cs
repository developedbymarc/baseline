using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using backend.Entities;

namespace backend.Data;

public sealed class BaselineLockedException(string message) : InvalidOperationException(message);

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<CrewMember> CrewMembers => Set<CrewMember>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<ReminderRule> ReminderRules => Set<ReminderRule>();
    public DbSet<ReminderInstance> ReminderInstances => Set<ReminderInstance>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<EnvironmentLog> EnvironmentLogs => Set<EnvironmentLog>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();
    public DbSet<ProtocolRun> ProtocolRuns => Set<ProtocolRun>();
    public DbSet<LinkMessage> LinkMessages => Set<LinkMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite cannot compare or order DateTimeOffset columns. Store them as UTC ticks (long),
        // which keeps WHERE / ORDER BY working. All times in the app are UTC.
        builder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        builder.Properties<DateTimeOffset?>().HaveConversion<UtcTicksConverter>();

        // Enums as readable strings in the DB file.
        builder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Mission>()
            .HasMany(m => m.Crew).WithOne(c => c.Mission).HasForeignKey(c => c.MissionId);

        b.Entity<Measurement>(e =>
        {
            e.HasIndex(x => new { x.CrewMemberId, x.MetricCode, x.RecordedAt });
        });

        b.Entity<Baseline>(e =>
        {
            e.HasKey(x => new { x.CrewMemberId, x.MetricCode });
            e.Ignore(x => x.IsLocked);
        });

        b.Entity<ReminderInstance>(e =>
        {
            e.HasIndex(x => new { x.RuleId, x.DueAt }).IsUnique();
        });

        b.Entity<LinkMessage>(e =>
        {
            e.HasIndex(x => new { x.MissionId, x.DeliveredAt });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardLockedBaselines();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        GuardLockedBaselines();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    // A4 / section 16: baselines are locked at mission start and no code path may edit them.
    private void GuardLockedBaselines()
    {
        foreach (var entry in ChangeTracker.Entries<Baseline>())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            var wasLocked = entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(Baseline.LockedAt)) is not null;
            if (wasLocked)
                throw new BaselineLockedException(
                    $"Baseline {entry.Entity.CrewMemberId}/{entry.Entity.MetricCode} is locked and cannot be changed.");
        }
    }

    internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));
}
