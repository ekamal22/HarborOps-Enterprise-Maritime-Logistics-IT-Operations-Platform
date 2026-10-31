using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261031010000_IdentitySessions")]
public sealed class IdentitySessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE users (
                id uuid PRIMARY KEY,
                email varchar(320) NOT NULL,
                normalized_email varchar(320) NOT NULL,
                display_name varchar(160) NOT NULL,
                status varchar(16) NOT NULL DEFAULT 'Active',
                security_version bigint NOT NULL DEFAULT 1,
                created_at_utc timestamptz NOT NULL,
                disabled_at_utc timestamptz NULL,
                CONSTRAINT "CK_users_status" CHECK (status IN ('Active', 'Disabled')),
                CONSTRAINT "CK_users_security_version" CHECK (security_version > 0)
            );
            CREATE UNIQUE INDEX "IX_users_NormalizedEmail" ON users (normalized_email);

            CREATE TABLE user_credentials (
                user_id uuid PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
                password_hash text NOT NULL,
                hash_algorithm varchar(24) NOT NULL,
                password_changed_at_utc timestamptz NOT NULL,
                created_at_utc timestamptz NOT NULL
            );

            CREATE TABLE auth_sessions (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                issued_at_utc timestamptz NOT NULL,
                last_seen_at_utc timestamptz NOT NULL,
                idle_expires_at_utc timestamptz NOT NULL,
                absolute_expires_at_utc timestamptz NOT NULL,
                security_version bigint NOT NULL,
                revoked_at_utc timestamptz NULL,
                revocation_reason varchar(40) NULL,
                CONSTRAINT "CK_auth_session_times" CHECK (
                    issued_at_utc <= last_seen_at_utc
                    AND last_seen_at_utc <= absolute_expires_at_utc
                    AND issued_at_utc < idle_expires_at_utc
                    AND idle_expires_at_utc <= absolute_expires_at_utc
                )
            );
            CREATE INDEX "IX_auth_sessions_UserId_RevokedAtUtc"
                ON auth_sessions (user_id, revoked_at_utc);

            CREATE TABLE auth_events (
                id uuid PRIMARY KEY,
                user_id uuid NULL REFERENCES users(id) ON DELETE SET NULL,
                session_id uuid NULL,
                event_type varchar(40) NOT NULL,
                occurred_at_utc timestamptz NOT NULL,
                correlation_id varchar(64) NOT NULL,
                result_code varchar(40) NOT NULL
            );
            CREATE INDEX "IX_auth_events_user_id" ON auth_events (user_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS auth_events;
            DROP TABLE IF EXISTS auth_sessions;
            DROP TABLE IF EXISTS user_credentials;
            DROP TABLE IF EXISTS users;
            """);
    }
}
