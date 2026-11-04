using HarborOps.Infrastructure.Identity;
using HarborOps.Infrastructure.Organizations;
using HarborOps.Infrastructure.References;
using HarborOps.Infrastructure.Maritime;
using HarborOps.Infrastructure.Shipments;
using HarborOps.Infrastructure.Assignments;
using HarborOps.Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace HarborOps.Infrastructure.Persistence;

public sealed class HarborOpsDbContext(DbContextOptions<HarborOpsDbContext> options) : DbContext(options)
{
    public DbSet<BootstrapRecord> BootstrapRecords => Set<BootstrapRecord>();
    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<PortReference> Ports => Set<PortReference>();
    public DbSet<VesselReference> Vessels => Set<VesselReference>();
    public DbSet<VoyageRecord> Voyages => Set<VoyageRecord>();
    public DbSet<ContainerRecord> Containers => Set<ContainerRecord>();
    public DbSet<ShipmentRecord> Shipments => Set<ShipmentRecord>();
    public DbSet<ShipmentStatusChange> ShipmentStatusChanges => Set<ShipmentStatusChange>();
    public DbSet<ShipmentContainerAssignment> ShipmentContainerAssignments => Set<ShipmentContainerAssignment>();
    public DbSet<ShipmentVoyageAssignment> ShipmentVoyageAssignments => Set<ShipmentVoyageAssignment>();

    public DbSet<IntegrationSource> IntegrationSources => Set<IntegrationSource>();
    public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();
    public DbSet<TrackingInboxReceipt> TrackingInboxReceipts => Set<TrackingInboxReceipt>();
    public DbSet<TrackingAcceptedEvent> TrackingAcceptedEvents => Set<TrackingAcceptedEvent>();
    public DbSet<TrackingInboxWorkItem> TrackingInboxWorkItems => Set<TrackingInboxWorkItem>();
    public DbSet<TrackingReceiptConflict> TrackingReceiptConflicts => Set<TrackingReceiptConflict>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntegrationSource>(entity =>
        {
            entity.ToTable("integration_sources", t =>
            {
                t.HasCheckConstraint("CK_m7_source_status", "status IN ('Active','Disabled')");
                t.HasCheckConstraint("CK_m7_source_name", "length(btrim(display_name)) BETWEEN 1 AND 160 AND display_name = btrim(display_name) AND display_name !~ '[[:cntrl:]]'");
                t.HasCheckConstraint("CK_m7_source_code", "source_code ~ '^[a-z][a-z0-9_-]{2,79}$' AND source_code !~ '[[:cntrl:]]'");
                t.HasCheckConstraint("CK_m7_source_version", "version > 0 AND updated_at_utc >= created_at_utc");
                t.HasCheckConstraint("CK_m7_source_disabled", "(status = 'Active' AND disabled_at_utc IS NULL) OR (status = 'Disabled' AND disabled_at_utc IS NOT NULL)");
                t.HasCheckConstraint("CK_m7_source_capability", "capability = 'tracking_event_validate'");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id })
                .HasName("AK_integration_sources_organization_id_id");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.SourceCode).HasColumnName("source_code").HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.SourceCode }).IsUnique().HasDatabaseName("UQ_m7_source_code");
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.SourceCode }).HasDatabaseName("IX_m7_source_status");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Capability).HasColumnName("capability").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.DisabledAtUtc).HasColumnName("disabled_at_utc").HasColumnType("timestamp with time zone");
        });
        modelBuilder.Entity<IntegrationCredential>(entity =>
        {
            entity.ToTable("integration_credentials", t =>
            {
                t.HasCheckConstraint("CK_m7_digest", "octet_length(secret_digest) = 32");
                t.HasCheckConstraint("CK_m7_version", "key_version = 'v1'");
                t.HasCheckConstraint("CK_m7_credential_window", "expires_at_utc > not_before_utc AND expires_at_utc <= not_before_utc + interval '90 days' AND created_at_utc <= not_before_utc AND (revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc)");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .HasName("AK_m8_integration_credential_scope");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.HasOne<IntegrationSource>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m7_credential_source");
            entity.Property(x => x.SecretDigest).HasColumnName("secret_digest").IsRequired();
            entity.Property(x => x.KeyVersion).HasColumnName("key_version").HasMaxLength(24).IsRequired();
            entity.Property(x => x.NotBeforeUtc).HasColumnName("not_before_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastUsedAtUtc).HasColumnName("last_used_at_utc").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.RevokedAtUtc }).HasDatabaseName("IX_m7_credential_scope");
            entity.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_m7_credential_expiry");
        });

        modelBuilder.Entity<TrackingInboxReceipt>(entity =>
        {
            entity.ToTable("tracking_inbox_receipts", t =>
            {
                t.HasCheckConstraint("CK_m8_receipt_digest", "octet_length(canonical_digest) = 32");
                t.HasCheckConstraint("CK_m8_receipt_canonical", "octet_length(canonical_payload_utf8) BETWEEN 1 AND 16384");
                t.HasCheckConstraint("CK_m8_receipt_profile", "profile = 'tnt-2.2-harborops-v1'");
                t.HasCheckConstraint("CK_m8_receipt_event_id", "source_event_id ~ '^[A-Za-z0-9._:-]{1,128}$'");
                t.HasCheckConstraint("CK_m8_receipt_correlation", "length(correlation_id) BETWEEN 1 AND 64");
            });
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .HasName("AK_m8_receipt_scope");
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.Property(x => x.CredentialId).HasColumnName("credential_id");
            entity.HasOne<IntegrationSource>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m8_receipt_source");
            entity.HasOne<IntegrationCredential>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId, x.CredentialId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m8_receipt_credential");
            entity.Property(x => x.Profile).HasColumnName("profile").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(128).IsRequired();
            entity.Property(x => x.CanonicalDigest).HasColumnName("canonical_digest").IsRequired();
            entity.Property(x => x.CanonicalPayloadUtf8).HasColumnName("canonical_payload_utf8").IsRequired();
            entity.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.Profile, x.SourceEventId })
                .IsUnique().HasDatabaseName("UQ_m8_receipt_key");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.ReceivedAtUtc })
                .HasDatabaseName("IX_m8_receipt_chronology");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.CredentialId })
                .HasDatabaseName("IX_m8_receipt_credential");
        });
        modelBuilder.Entity<TrackingAcceptedEvent>(entity =>
        {
            entity.ToTable("tracking_accepted_events", t =>
            {
                t.HasCheckConstraint("CK_m8_event_payload", "octet_length(raw_payload_utf8) BETWEEN 1 AND 16384");
                t.HasCheckConstraint("CK_m8_event_variant", "classifier = 'ACT' AND external_schema_version = '2.2.0' AND ((event_family = 'Equipment' AND event_code IN ('GTIN', 'GTOT') AND equipment_reference IS NOT NULL AND transport_reference IS NULL) OR (event_family = 'Transport' AND event_code IN ('ARRI', 'DEPA') AND transport_reference IS NOT NULL AND equipment_reference IS NULL))");
                t.HasCheckConstraint("CK_m8_event_canonical", "jsonb_typeof(canonical_payload) = 'object'");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.ReceiptId).HasColumnName("receipt_id");
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.HasOne<TrackingInboxReceipt>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m8_event_receipt");
            entity.HasIndex(x => x.ReceiptId).IsUnique().HasDatabaseName("UQ_m8_event_receipt");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasDatabaseName("IX_m8_event_scope");
            entity.Property(x => x.EventFamily).HasColumnName("event_family").HasMaxLength(16).IsRequired();
            entity.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(8).IsRequired();
            entity.Property(x => x.Classifier).HasColumnName("classifier").HasMaxLength(8).IsRequired();
            entity.Property(x => x.ExternalSchemaVersion).HasColumnName("external_schema_version").HasMaxLength(16).IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.EquipmentReference).HasColumnName("equipment_reference").HasMaxLength(32);
            entity.Property(x => x.TransportReference).HasColumnName("transport_reference").HasMaxLength(80);
            entity.Property(x => x.RawPayloadUtf8).HasColumnName("raw_payload_utf8").IsRequired();
            entity.Property(x => x.CanonicalPayloadJson).HasColumnName("canonical_payload").HasColumnType("jsonb").IsRequired();
        });
        modelBuilder.Entity<TrackingInboxWorkItem>(entity =>
        {
            entity.ToTable("tracking_inbox_work_items", t =>
            {
                t.HasCheckConstraint("CK_m8_work_state", "state = 'Pending' AND attempt_count = 0 AND updated_at_utc >= created_at_utc");
            });
            entity.HasKey(x => x.ReceiptId);
            entity.Property(x => x.ReceiptId).HasColumnName("receipt_id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.HasOne<TrackingInboxReceipt>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m8_work_receipt");
            entity.Property(x => x.State).HasColumnName("state").HasMaxLength(24).IsRequired();
            entity.Property(x => x.AttemptCount).HasColumnName("attempt_count");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.OrganizationId, x.State, x.CreatedAtUtc })
                .HasDatabaseName("IX_m8_work_pending");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasDatabaseName("IX_m8_work_scope");
        });
        modelBuilder.Entity<TrackingReceiptConflict>(entity =>
        {
            entity.ToTable("tracking_receipt_conflicts", t =>
            {
                t.HasCheckConstraint("CK_m8_conflict_digest", "octet_length(attempted_digest) = 32");
                t.HasCheckConstraint("CK_m8_conflict_correlation", "length(correlation_id) BETWEEN 1 AND 64");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.ReceiptId).HasColumnName("receipt_id");
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.HasOne<TrackingInboxReceipt>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.SourceId, x.Id })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_m8_conflict_receipt");
            entity.HasIndex(x => x.ReceiptId).IsUnique().HasDatabaseName("UQ_m8_conflict_receipt");
            entity.HasIndex(x => new { x.OrganizationId, x.SourceId, x.ReceiptId })
                .HasDatabaseName("IX_m8_conflict_scope");
            entity.Property(x => x.AttemptedDigest).HasColumnName("attempted_digest").IsRequired();
            entity.Property(x => x.FirstSeenAtUtc).HasColumnName("first_seen_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64).IsRequired();
        });

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
            entity.Property(x => x.SelectedOrganizationId).HasColumnName("selected_organization_id");
            entity.HasIndex(x => x.SelectedOrganizationId);
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.SelectedOrganizationId).OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(x => x.UserId).HasDatabaseName("IX_auth_events_user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResultCode).HasColumnName("result_code").HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations", t =>
            {
                t.HasCheckConstraint("CK_organizations_status", "status IN ('Active', 'Suspended')");
                t.HasCheckConstraint("CK_organizations_version", "version > 0");
                t.HasCheckConstraint("CK_organizations_slug",
                    "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND length(slug) BETWEEN 3 AND 80");
                t.HasCheckConstraint("CK_organizations_display_name",
                    "length(btrim(display_name)) BETWEEN 1 AND 160");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        });
        modelBuilder.Entity<OrganizationMembership>(entity =>
        {
            entity.ToTable("organization_memberships", t =>
            {
                t.HasCheckConstraint("CK_membership_role", "role IN ('Analyst','Manager','Support','Administrator','Auditor')");
                t.HasCheckConstraint("CK_membership_status", "status IN ('Active','Suspended','Removed')");
                t.HasCheckConstraint("CK_membership_version", "version > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id");
            entity.HasIndex(x => x.GrantedByUserId);
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status, x.OrganizationId });
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.Role });
            entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(24).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        });

        modelBuilder.Ignore<ReferenceRecord>();
        modelBuilder.Entity<PortReference>(entity =>
        {
            ConfigureReference(entity, "ports");
            entity.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
            entity.Property(x => x.UnLocode).HasColumnName("un_locode").HasMaxLength(5);
            entity.HasIndex(x => new { x.OrganizationId, x.UnLocode })
                .IsUnique().HasFilter("un_locode IS NOT NULL");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ports_country", "country_code ~ '^[A-Z]{2}$'");
                t.HasCheckConstraint("CK_ports_locode",
                    "un_locode IS NULL OR (un_locode ~ '^[A-Z]{2}[A-Z2-9]{3}$' AND substring(un_locode,1,2) = country_code)");
            });
        });
        modelBuilder.Entity<VesselReference>(entity =>
        {
            ConfigureReference(entity, "vessels");
            entity.Property(x => x.ImoNumber).HasColumnName("imo_number").HasMaxLength(7);
            entity.Property(x => x.Mmsi).HasColumnName("mmsi").HasMaxLength(9);
            entity.Property(x => x.FlagCountryCode).HasColumnName("flag_country_code").HasMaxLength(2);
            entity.HasIndex(x => new { x.OrganizationId, x.ImoNumber })
                .IsUnique().HasFilter("imo_number IS NOT NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.Mmsi })
                .IsUnique().HasFilter("mmsi IS NOT NULL");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_vessels_mmsi", "mmsi IS NULL OR (mmsi ~ '^[0-9]{9}$' AND mmsi <> '000000000')");
                t.HasCheckConstraint("CK_vessels_flag", "flag_country_code IS NULL OR flag_country_code ~ '^[A-Z]{2}$'");
                t.HasCheckConstraint("CK_vessels_imo", "imo_number IS NULL OR (imo_number ~ '^[0-9]{7}$' AND (substring(imo_number,1,1)::int * 7 + substring(imo_number,2,1)::int * 6 + substring(imo_number,3,1)::int * 5 + substring(imo_number,4,1)::int * 4 + substring(imo_number,5,1)::int * 3 + substring(imo_number,6,1)::int * 2) % 10 = substring(imo_number,7,1)::int)");
            });
        });

        modelBuilder.Ignore<MaritimeRecord>();
        modelBuilder.Entity<VoyageRecord>(entity =>
        {
            ConfigureMaritime(entity, "voyages");
            entity.Property(x => x.VesselId).HasColumnName("vessel_id");
            entity.Property(x => x.OriginPortId).HasColumnName("origin_port_id");
            entity.Property(x => x.DestinationPortId).HasColumnName("destination_port_id");
            entity.Property(x => x.VoyageStatus).HasColumnName("voyage_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.PlannedDepartureAtUtc).HasColumnName("planned_departure_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.PlannedArrivalAtUtc).HasColumnName("planned_arrival_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ActualDepartureAtUtc).HasColumnName("actual_departure_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ActualArrivalAtUtc).HasColumnName("actual_arrival_at_utc").HasColumnType("timestamp with time zone");
            entity.HasOne<VesselReference>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.VesselId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PortReference>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OriginPortId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PortReference>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DestinationPortId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.VesselId })
                .HasDatabaseName("IX_voyages_OrganizationId_VesselId");
            entity.HasIndex(x => new { x.OrganizationId, x.OriginPortId })
                .HasDatabaseName("IX_voyages_OrganizationId_OriginPortId");
            entity.HasIndex(x => new { x.OrganizationId, x.DestinationPortId })
                .HasDatabaseName("IX_voyages_OrganizationId_DestinationPortId");
            entity.HasIndex(x => new { x.OrganizationId, x.VoyageStatus, x.Id });
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_voyages_route", "origin_port_id <> destination_port_id");
                t.HasCheckConstraint("CK_voyages_progress", "voyage_status IN ('Planned','Underway','Arrived','Cancelled')");
                t.HasCheckConstraint("CK_voyages_planned", "planned_departure_at_utc IS NULL OR planned_arrival_at_utc IS NULL OR planned_departure_at_utc < planned_arrival_at_utc");
                t.HasCheckConstraint("CK_voyages_actual", "actual_departure_at_utc IS NULL OR actual_arrival_at_utc IS NULL OR actual_departure_at_utc <= actual_arrival_at_utc");
                t.HasCheckConstraint("CK_voyages_progress_time",
                    "(voyage_status = 'Planned' AND actual_departure_at_utc IS NULL AND actual_arrival_at_utc IS NULL) OR " +
                    "(voyage_status = 'Underway' AND actual_departure_at_utc IS NOT NULL AND actual_arrival_at_utc IS NULL) OR " +
                    "(voyage_status = 'Arrived' AND actual_departure_at_utc IS NOT NULL AND actual_arrival_at_utc IS NOT NULL) OR " +
                    "(voyage_status = 'Cancelled' AND actual_departure_at_utc IS NULL AND actual_arrival_at_utc IS NULL)");
            });
        });
        modelBuilder.Entity<ContainerRecord>(entity =>
        {
            ConfigureMaritime(entity, "containers");
            entity.Property(x => x.IdentifierKind).HasColumnName("identifier_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.ContainerIdentifier).HasColumnName("container_identifier").HasMaxLength(32).IsRequired();
            entity.Property(x => x.EquipmentType).HasColumnName("equipment_type").HasMaxLength(20).IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160);
            entity.HasIndex(x => new { x.OrganizationId, x.ContainerIdentifier }).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_containers_kind", "identifier_kind IN ('ISO6346','Synthetic')");
                t.HasCheckConstraint("CK_containers_type", "equipment_type IN ('Dry20','Dry40','HighCube40','Reefer20','Reefer40','Other')");
                t.HasCheckConstraint("CK_containers_identifier",
                    "(identifier_kind = 'ISO6346' AND harborops_iso6346_valid(container_identifier) AND source_kind='UserProvided') OR " +
                    "(identifier_kind='Synthetic' AND container_identifier ~ '^SYN-BOX-[A-Z0-9]{4,16}$' AND source_kind='Synthetic')");
                t.HasCheckConstraint("CK_containers_display", "display_name IS NULL OR length(btrim(display_name)) BETWEEN 1 AND 160");
            });
        });
        modelBuilder.Ignore<AssignmentRecord>();
        modelBuilder.Entity<ContainerRecord>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        modelBuilder.Entity<VoyageRecord>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        modelBuilder.Entity<ShipmentContainerAssignment>(entity =>
        {
            ConfigureAssignment(entity, "shipment_container_assignments");
            entity.Property(x => x.ContainerId).HasColumnName("container_id");
            entity.HasOne<ContainerRecord>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ContainerId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.ContainerId, x.StartsAtUtc, x.Id })
                .HasDatabaseName("IX_m6_container_target");
            entity.HasIndex(x => new { x.OrganizationId, x.ShipmentId, x.StartsAtUtc, x.Id })
                .HasDatabaseName("IX_m6_container_shipment");
            entity.HasIndex(x => x.CreatedByUserId).HasDatabaseName("IX_m6_container_created_by");
            entity.HasIndex(x => x.ClosedByUserId).HasDatabaseName("IX_m6_container_closed_by");
        });
        modelBuilder.Entity<ShipmentVoyageAssignment>(entity =>
        {
            ConfigureAssignment(entity, "shipment_voyage_assignments");
            entity.Property(x => x.VoyageId).HasColumnName("voyage_id");
            entity.HasOne<VoyageRecord>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.VoyageId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.VoyageId, x.StartsAtUtc, x.Id })
                .HasDatabaseName("IX_m6_voyage_target");
            entity.HasIndex(x => new { x.OrganizationId, x.ShipmentId, x.StartsAtUtc, x.Id })
                .HasDatabaseName("IX_m6_voyage_shipment");
            entity.HasIndex(x => x.CreatedByUserId).HasDatabaseName("IX_m6_voyage_created_by");
            entity.HasIndex(x => x.ClosedByUserId).HasDatabaseName("IX_m6_voyage_closed_by");
        });
        modelBuilder.Entity<ShipmentRecord>(entity =>
        {
            entity.ToTable("shipments", t =>
            {
                t.HasCheckConstraint("CK_shipments_code", "reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32");
                t.HasCheckConstraint("CK_shipments_external", "external_reference IS NULL OR external_reference ~ '^[A-Z0-9._/-]{1,80}$'");
                t.HasCheckConstraint("CK_shipments_display", "display_name IS NULL OR (length(btrim(display_name)) BETWEEN 1 AND 160 AND display_name !~ '[[:cntrl:]]')");
                t.HasCheckConstraint("CK_shipments_route", "origin_port_id <> destination_port_id");
                t.HasCheckConstraint("CK_shipments_times", "planned_departure_at_utc IS NULL OR planned_arrival_at_utc IS NULL OR planned_departure_at_utc < planned_arrival_at_utc");
                t.HasCheckConstraint("CK_shipments_workflow", "workflow_status IN ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')");
                t.HasCheckConstraint("CK_shipments_status", "record_status IN ('Active','Archived')");
                t.HasCheckConstraint("CK_shipments_source", "source_kind IN ('UserProvided','Synthetic')");
                t.HasCheckConstraint("CK_shipments_version", "version > 0");
                t.HasCheckConstraint("CK_shipments_archive", "(record_status='Active' AND archived_at_utc IS NULL) OR (record_status='Archived' AND archived_at_utc IS NOT NULL)");
                t.HasCheckConstraint("CK_shipments_time", "updated_at_utc >= created_at_utc");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(80);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160);
            entity.Property(x => x.OriginPortId).HasColumnName("origin_port_id");
            entity.Property(x => x.DestinationPortId).HasColumnName("destination_port_id");
            entity.HasOne<PortReference>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OriginPortId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PortReference>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DestinationPortId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.PlannedDepartureAtUtc).HasColumnName("planned_departure_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.PlannedArrivalAtUtc).HasColumnName("planned_arrival_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.WorkflowStatus).HasColumnName("workflow_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.RecordStatus).HasColumnName("record_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.SourceKind).HasColumnName("source_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ArchivedAtUtc).HasColumnName("archived_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ChangedByUserId).HasDatabaseName("IX_shipments_changed_by_user_id");
            entity.HasIndex(x => new { x.OrganizationId, x.ReferenceCode }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.ExternalReference }).IsUnique()
                .HasFilter("external_reference IS NOT NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.RecordStatus, x.Id });
            entity.HasIndex(x => new { x.OrganizationId, x.WorkflowStatus, x.Id });
            entity.HasIndex(x => new { x.OrganizationId, x.OriginPortId });
            entity.HasIndex(x => new { x.OrganizationId, x.DestinationPortId });
        });
        modelBuilder.Entity<ShipmentStatusChange>(entity =>
        {
            entity.ToTable("shipment_status_changes", t =>
            {
                t.HasCheckConstraint("CK_shipment_status_changes_to", "to_status IN ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')");
                t.HasCheckConstraint("CK_shipment_status_changes_from", "from_status IS NULL OR from_status IN ('Draft','Booked','InTransit','Arrived','Completed','Cancelled')");
                t.HasCheckConstraint("CK_shipment_status_changes_version", "version > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
            entity.Property(x => x.ShipmentId).HasColumnName("shipment_id");
            entity.HasOne<ShipmentRecord>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ShipmentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(16);
            entity.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.ChangedAtUtc).HasColumnName("changed_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ChangedByUserId).HasDatabaseName("IX_shipment_status_changes_changed_by_user_id");
            entity.HasIndex(x => new { x.OrganizationId, x.ShipmentId, x.Version }).IsUnique();
        });
    }

    private static void ConfigureAssignment<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : AssignmentRecord
    {
        entity.ToTable(table, t =>
        {
            t.HasCheckConstraint(table == "shipment_container_assignments"
                ? "CK_m6_container_window" : "CK_m6_voyage_window",
                "ends_at_utc IS NULL OR ends_at_utc > starts_at_utc");
            t.HasCheckConstraint(table == "shipment_container_assignments"
                ? "CK_m6_container_version" : "CK_m6_voyage_version",
                "version > 0 AND updated_at_utc >= created_at_utc");
            t.HasCheckConstraint(table == "shipment_container_assignments"
                ? "CK_m6_container_closure" : "CK_m6_voyage_closure",
                "(ends_at_utc IS NULL AND closed_by_user_id IS NULL) OR ends_at_utc IS NOT NULL");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.ShipmentId).HasColumnName("shipment_id");
        entity.HasOne<ShipmentRecord>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ShipmentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.StartsAtUtc).HasColumnName("starts_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(x => x.EndsAtUtc).HasColumnName("ends_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");
        entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        entity.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
        entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<AuthUser>().WithMany().HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureMaritime<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : MaritimeRecord
    {
        entity.ToTable(table, t =>
        {
            t.HasCheckConstraint("CK_" + table + "_status", "record_status IN ('Active','Archived')");
            t.HasCheckConstraint("CK_" + table + "_source", "source_kind IN ('UserProvided','Synthetic')");
            t.HasCheckConstraint("CK_" + table + "_version", "version > 0");
            t.HasCheckConstraint("CK_" + table + "_code", "reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32");
            t.HasCheckConstraint("CK_" + table + "_timestamp", "updated_at_utc >= created_at_utc");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.HasOne<HarborOps.Infrastructure.Organizations.Organization>().WithMany()
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.OrganizationId, x.ReferenceCode }).IsUnique();
        entity.HasIndex(x => new { x.OrganizationId, x.Id });
        entity.Property(x => x.RecordStatus).HasColumnName("record_status").HasMaxLength(16).IsRequired();
        entity.Property(x => x.SourceKind).HasColumnName("source_kind").HasMaxLength(16).IsRequired();
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
    }

    private static void ConfigureReference<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : ReferenceRecord
    {
        entity.ToTable(table, t =>
        {
            t.HasCheckConstraint("CK_" + table + "_status", "status IN ('Active','Archived')");
            t.HasCheckConstraint("CK_" + table + "_source_kind", "source_kind IN ('UserProvided','Synthetic')");
            t.HasCheckConstraint("CK_" + table + "_version", "version > 0");
            t.HasCheckConstraint("CK_" + table + "_code", "reference_code ~ '^[A-Z0-9]+(-[A-Z0-9]+)*$' AND length(reference_code) BETWEEN 3 AND 32");
            t.HasCheckConstraint("CK_" + table + "_name", "length(btrim(name)) BETWEEN 1 AND 160");
            t.HasCheckConstraint("CK_" + table + "_time", "updated_at_utc >= created_at_utc");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.HasOne<HarborOps.Infrastructure.Organizations.Organization>().WithMany()
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(32).IsRequired();
        entity.HasIndex(x => new { x.OrganizationId, x.ReferenceCode }).IsUnique();
        entity.HasIndex(x => new { x.OrganizationId, x.Id });
        entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        entity.Property(x => x.SourceKind).HasColumnName("source_kind").HasMaxLength(16).IsRequired();
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
    }
}
