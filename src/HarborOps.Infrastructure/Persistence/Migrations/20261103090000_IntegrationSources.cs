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
                CONSTRAINT "CK_m7_source_name" CHECK (length(btrim(display_name)) BETWEEN 1 AND 160 AND
                    display_name = btrim(display_name) AND display_name !~ '[[:cntrl:]]'),
                CONSTRAINT "CK_m7_source_code" CHECK (source_code ~ '^[a-z][a-z0-9_-]{2,79}$' AND
                    source_code !~ '[[:cntrl:]]'),
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
                     created_at_utc <= not_before_utc AND
                     (revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc))
            );
            CREATE INDEX "IX_m7_credential_scope" ON integration_credentials
                (organization_id,source_id,revoked_at_utc);
            CREATE INDEX "IX_m7_credential_expiry" ON integration_credentials(expires_at_utc);

            CREATE FUNCTION m7_guard_source() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Integration source history cannot be deleted' USING ERRCODE='23514';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    IF NEW.status <> 'Active' OR NEW.disabled_at_utc IS NOT NULL OR NEW.version <> 1 OR
                       NEW.created_at_utc > clock_timestamp() + interval '5 minutes' THEN
                        RAISE EXCEPTION 'Invalid source creation' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF NEW.id IS DISTINCT FROM OLD.id OR
                   NEW.organization_id IS DISTINCT FROM OLD.organization_id OR
                   NEW.source_code IS DISTINCT FROM OLD.source_code OR
                   NEW.capability IS DISTINCT FROM OLD.capability OR
                   NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc OR
                   NEW.version <> OLD.version + 1 OR
                   NEW.updated_at_utc < OLD.updated_at_utc OR
                   (OLD.status = 'Disabled' AND NEW.status <> 'Disabled') OR
                   (OLD.status = 'Active' AND NEW.status = 'Disabled' AND NEW.disabled_at_utc IS NULL) OR
                   (NEW.status = 'Active' AND NEW.disabled_at_utc IS NOT NULL) OR
                   (OLD.disabled_at_utc IS NOT NULL AND NEW.disabled_at_utc IS DISTINCT FROM OLD.disabled_at_utc)
                THEN
                    RAISE EXCEPTION 'Invalid integration source update' USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m7_source_guard BEFORE INSERT OR UPDATE OR DELETE ON integration_sources
                FOR EACH ROW EXECUTE FUNCTION m7_guard_source();

            CREATE FUNCTION m7_guard_credential() RETURNS trigger LANGUAGE plpgsql AS $fn$
            DECLARE overlapping_count bigint;
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Integration credential history cannot be deleted' USING ERRCODE='23514';
                END IF;
                IF TG_OP = 'INSERT' THEN
                    -- Serialize even direct SQL issuance on the same source row.
                    PERFORM 1 FROM integration_sources
                     WHERE id = NEW.source_id AND organization_id = NEW.organization_id
                     FOR UPDATE;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Source not found' USING ERRCODE='23503';
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM integration_sources s
                        JOIN organizations o ON o.id = s.organization_id
                        WHERE s.id = NEW.source_id AND s.organization_id = NEW.organization_id
                          AND s.status='Active' AND o.status='Active'
                    ) OR NEW.revoked_at_utc IS NOT NULL OR
                       NEW.not_before_utc < NEW.created_at_utc OR
                       NEW.created_at_utc > clock_timestamp() + interval '5 minutes' THEN
                        RAISE EXCEPTION 'Invalid credential issuance' USING ERRCODE='23514';
                    END IF;
                    SELECT count(*) INTO overlapping_count
                      FROM integration_credentials c
                     WHERE c.source_id=NEW.source_id
                       AND c.organization_id=NEW.organization_id
                       AND c.revoked_at_utc IS NULL
                       AND c.not_before_utc < NEW.expires_at_utc
                       AND c.expires_at_utc > NEW.not_before_utc;
                    IF overlapping_count >= 2 THEN
                        RAISE EXCEPTION 'Credential overlap limit exceeded' USING ERRCODE='23514';
                    END IF;
                    RETURN NEW;
                END IF;
                IF NEW.id IS DISTINCT FROM OLD.id OR
                   NEW.organization_id IS DISTINCT FROM OLD.organization_id OR
                   NEW.source_id IS DISTINCT FROM OLD.source_id OR
                   NEW.secret_digest IS DISTINCT FROM OLD.secret_digest OR
                   NEW.key_version IS DISTINCT FROM OLD.key_version OR
                   NEW.not_before_utc IS DISTINCT FROM OLD.not_before_utc OR
                   NEW.created_at_utc IS DISTINCT FROM OLD.created_at_utc OR
                   NEW.expires_at_utc > OLD.expires_at_utc OR
                   (OLD.revoked_at_utc IS NOT NULL AND NEW.revoked_at_utc IS DISTINCT FROM OLD.revoked_at_utc) OR
                   (OLD.last_used_at_utc IS NOT NULL AND
                       (NEW.last_used_at_utc IS NULL OR NEW.last_used_at_utc < OLD.last_used_at_utc))
                THEN
                    RAISE EXCEPTION 'Immutable credential identity or invalid lifecycle transition'
                        USING ERRCODE='23514';
                END IF;
                RETURN NEW;
            END $fn$;
            CREATE TRIGGER m7_credential_guard BEFORE INSERT OR UPDATE OR DELETE ON integration_credentials
                FOR EACH ROW EXECUTE FUNCTION m7_guard_credential();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE integration_credentials;
            DROP TABLE integration_sources;
            DROP FUNCTION m7_guard_credential();
            DROP FUNCTION m7_guard_source();
            """);
    }
}
