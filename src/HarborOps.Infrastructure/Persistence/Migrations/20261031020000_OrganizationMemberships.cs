using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261031020000_OrganizationMemberships")]
public sealed class OrganizationMemberships : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE organizations (
                id uuid PRIMARY KEY,
                slug varchar(80) NOT NULL,
                display_name varchar(160) NOT NULL,
                status varchar(16) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                CONSTRAINT "CK_organizations_status" CHECK (status IN ('Active', 'Suspended')),
                CONSTRAINT "CK_organizations_version" CHECK (version > 0),
                CONSTRAINT "CK_organizations_slug" CHECK (
                    slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$' AND length(slug) BETWEEN 3 AND 80),
                CONSTRAINT "CK_organizations_display_name" CHECK (
                    length(btrim(display_name)) BETWEEN 1 AND 160)
            );
            CREATE UNIQUE INDEX "IX_organizations_Slug" ON organizations (slug);

            CREATE TABLE organization_memberships (
                id uuid PRIMARY KEY,
                organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
                user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                role varchar(24) NOT NULL,
                status varchar(16) NOT NULL,
                version bigint NOT NULL,
                created_at_utc timestamptz NOT NULL,
                updated_at_utc timestamptz NOT NULL,
                granted_by_user_id uuid NULL REFERENCES users(id) ON DELETE RESTRICT,
                CONSTRAINT "CK_membership_role" CHECK (role IN ('Analyst', 'Manager', 'Support', 'Administrator', 'Auditor')),
                CONSTRAINT "CK_membership_status" CHECK (status IN ('Active', 'Suspended', 'Removed')),
                CONSTRAINT "CK_membership_version" CHECK (version > 0)
            );
            CREATE UNIQUE INDEX "IX_organization_memberships_OrganizationId_UserId"
                ON organization_memberships (organization_id, user_id);
            CREATE INDEX "IX_organization_memberships_UserId_Status_OrganizationId"
                ON organization_memberships (user_id, status, organization_id);
            CREATE INDEX "IX_organization_memberships_OrganizationId_Status_Role"
                ON organization_memberships (organization_id, status, role);
            CREATE INDEX "IX_organization_memberships_GrantedByUserId"
                ON organization_memberships (granted_by_user_id);

            ALTER TABLE auth_sessions ADD COLUMN selected_organization_id uuid NULL;
            ALTER TABLE auth_sessions ADD CONSTRAINT "FK_auth_sessions_organizations_SelectedOrganizationId"
                FOREIGN KEY (selected_organization_id) REFERENCES organizations(id) ON DELETE RESTRICT;
            CREATE INDEX "IX_auth_sessions_SelectedOrganizationId"
                ON auth_sessions (selected_organization_id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE auth_sessions DROP CONSTRAINT "FK_auth_sessions_organizations_SelectedOrganizationId";
            DROP INDEX "IX_auth_sessions_SelectedOrganizationId";
            ALTER TABLE auth_sessions DROP COLUMN selected_organization_id;
            DROP TABLE organization_memberships;
            DROP TABLE organizations;
            """);
    }
}
