using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261103090000_IntegrationSources")]
public sealed class IntegrationSources : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE integration_sources (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                source_code varchar(80) NOT NULL,
                display_name varchar(160) NOT NULL,
                status varchar(16) NOT NULL DEFAULT 'Active',
                capability varchar(40) NOT NULL DEFAULT 'tracking_event_validate',
                version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                disabled_at_utc timestamptz NULL,
                CONSTRAINT "AK_integration_sources_organization_id_id" UNIQUE (organization_id,id),
                CONSTRAINT "UQ_m7_source_code" UNIQUE (organization_id,source_code),
                CONSTRAINT "CK_m7_source_status" CHECK (status IN ('Active','Disabled')),
                CONSTRAINT "CK_m7_source_code" CHECK (source_code ~ '^[a-z][a-z0-9_-]{2,79}$'),
                CONSTRAINT "CK_m7_source_version" CHECK (version > 0 AND updated_at_utc >= created_at_utc),
                CONSTRAINT "CK_m7_source_disabled" CHECK ((status = 'Active' AND disabled_at_utc IS NULL) OR
                    (status = 'Disabled' AND disabled_at_utc IS NOT NULL)),
                CONSTRAINT "CK_m7_source_capability" CHECK (capability = 'tracking_event_validate')
            );
            CREATE INDEX "IX_m7_source_status" ON integration_sources
                (organization_id,status,source_code);

            CREATE TABLE integration_credentials (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                source_id uuid NOT NULL,
                secret_digest bytea NOT NULL,
                key_version varchar(24) NOT NULL,
                not_before_utc timestamptz NOT NULL,
                expires_at_utc timestamptz NOT NULL,
                revoked_at_utc timestamptz NULL,
                created_at_utc timestamptz NOT NULL,
                last_used_at_utc timestamptz NULL,
                CONSTRAINT "FK_m7_credential_source" FOREIGN KEY (organization_id,source_id)
                    REFERENCES integration_sources(organization_id,id) ON DELETE RESTRICT,
                CONSTRAINT "CK_m7_digest" CHECK (octet_length(secret_digest) = 32),
                CONSTRAINT "CK_m7_version" CHECK (key_version = 'v1'),
                CONSTRAINT "CK_m7_credential_window" CHECK
                    (expires_at_utc > not_before_utc AND
                     expires_at_utc <= not_before_utc + interval '90 days' AND
                     created_at_utc <= expires_at_utc AND
                     (revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc))
            );
            CREATE INDEX "IX_m7_credential_scope" ON integration_credentials
                (organization_id,source_id,revoked_at_utc);
            CREATE INDEX "IX_m7_credential_expiry" ON integration_credentials(expires_at_utc);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE integration_credentials;
            DROP TABLE integration_sources;
            """);
    }
}
