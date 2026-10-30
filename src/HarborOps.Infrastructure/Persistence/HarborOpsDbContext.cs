using HarborOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Persistence;

public sealed class HarborOpsDbContext(DbContextOptions<HarborOpsDbContext> options) : DbContext(options)
{
    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();
    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();

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
        modelBuilder.Entity<AuthUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.SecurityVersion).HasColumnName("security_version");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.DisabledAtUtc).HasColumnName("disabled_at_utc").HasColumnType("timestamp with time zone");
            entity.ToTable(t => t.HasCheckConstraint("CK_users_status", "status IN ('Active','Disabled')"));
        });
        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.ToTable("user_credentials");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.HasOne<AuthUser>().WithOne().HasForeignKey<UserCredential>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(x => x.HashAlgorithm).HasColumnName("hash_algorithm").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PasswordChangedAtUtc).HasColumnName("password_changed_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        });
        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("auth_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
            entity.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastSeenAtUtc).HasColumnName("last_seen_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.IdleExpiresAtUtc).HasColumnName("idle_expires_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.SecurityVersion).HasColumnName("security_version");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(40);
            entity.ToTable(t => t.HasCheckConstraint("CK_auth_session_times",
                "issued_at_utc <= last_seen_at_utc AND last_seen_at_utc <= absolute_expires_at_utc AND issued_at_utc < idle_expires_at_utc AND idle_expires_at_utc <= absolute_expires_at_utc"));
        });
        modelBuilder.Entity<AuthEvent>(entity =>
        {
            entity.ToTable("auth_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(40).IsRequired();
        });
    }
}
