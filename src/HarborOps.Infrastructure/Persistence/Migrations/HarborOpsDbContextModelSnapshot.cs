using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
public sealed class HarborOpsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("HarborOps.Infrastructure.Persistence.BootstrapRecord", entity =>
        {
            entity.Property<string>("Key").HasMaxLength(80).HasColumnName("key");
            entity.Property<string>("DatasetVersion").IsRequired().HasMaxLength(80).HasColumnName("dataset_version");
            entity.Property<DateTimeOffset>("SeededAtUtc").HasColumnType("timestamp with time zone").HasColumnName("seeded_at_utc");
            entity.HasKey("Key");
            entity.ToTable("bootstrap_records");
        });
    }
}
