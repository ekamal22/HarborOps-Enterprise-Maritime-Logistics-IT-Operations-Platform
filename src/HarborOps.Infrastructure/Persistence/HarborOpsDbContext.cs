using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Persistence;

public sealed class HarborOpsDbContext(DbContextOptions<HarborOpsDbContext> options) : DbContext(options)
{
    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BootstrapRecord>(entity =>
        {
            entity.ToTable("bootstrap_records");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasColumnName("key").HasMaxLength(80);
            entity.Property(x => x.DatasetVersion).HasColumnName("dataset_version").HasMaxLength(80).IsRequired();
            entity.Property(x => x.SeededAtUtc).HasColumnName("seeded_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        });
    }
}
