using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Transactions.Core.Domain;

namespace Transactions.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no timezone-aware type and EF reads DateTime back as Kind=Unspecified.
        // Every DateTime in this model is UTC, so normalize on write and stamp Kind=Utc on read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions");
            e.HasKey(t => t.Id);

            // Logical key: TransactionId is only unique within a source system.
            e.HasIndex(t => new { t.SourceSystem, t.TransactionId }).IsUnique();
            e.HasIndex(t => new { t.SourceSystem, t.Status });
            e.HasIndex(t => t.TransactionTime);

            e.Property(t => t.SourceSystem).HasMaxLength(Transaction.MaxTextLength).IsRequired();
            e.Property(t => t.CardNumber).HasMaxLength(Transaction.MaxTextLength).IsRequired();
            e.Property(t => t.LocationCode).HasMaxLength(Transaction.MaxTextLength).IsRequired();
            e.Property(t => t.ProductName).HasMaxLength(Transaction.MaxTextLength).IsRequired();

            // decimal(18,2) semantics. SQLite has no decimal type (EF stores TEXT and cannot ORDER BY / compare it),
            // so we persist whole cents as INTEGER: exact, sortable and filterable in SQL.
            e.Property(t => t.Amount)
             .HasConversion(new ValueConverter<decimal, long>(
                 v => (long)decimal.Round(v * 100m, 0, MidpointRounding.AwayFromZero),
                 v => v / 100m));

            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);

            e.HasMany(t => t.AuditEntries)
             .WithOne(a => a.Transaction)
             .HasForeignKey(a => a.TransactionRecordId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransactionAudit>(e =>
        {
            e.ToTable("TransactionAudits");
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.FieldName).HasMaxLength(32);
            e.Property(a => a.OldValue).HasMaxLength(64);
            e.Property(a => a.NewValue).HasMaxLength(64);
            e.HasIndex(a => a.TransactionRecordId);
            e.HasIndex(a => a.ImportRunId);
            e.HasOne<ImportRun>()
             .WithMany()
             .HasForeignKey(a => a.ImportRunId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportRun>(e =>
        {
            e.ToTable("ImportRuns");
            e.HasKey(r => r.Id);
            e.Property(r => r.Trigger).HasMaxLength(32);
            e.Property(r => r.FailedSources).HasMaxLength(256);
            e.Ignore(r => r.Succeeded);
            e.HasIndex(r => r.StartedAtUtc);
        });
    }
}

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
