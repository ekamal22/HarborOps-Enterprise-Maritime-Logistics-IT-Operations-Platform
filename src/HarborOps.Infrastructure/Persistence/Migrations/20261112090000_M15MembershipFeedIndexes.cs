using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HarborOps.Infrastructure.Persistence.Migrations;

// These indexes retain all historical receipts while making the current
// membership version an indexed read predicate. No notification rows are deleted.
[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261112090000_M15MembershipFeedIndexes")]
public sealed class M15MembershipFeedIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE INDEX ix_m15_membership_feed ON m15_user_notifications
                (organization_id,recipient_user_id,membership_id,membership_version,
                 created_at_utc DESC,id DESC);
            CREATE INDEX ix_m15_membership_unread_feed ON m15_user_notifications
                (organization_id,recipient_user_id,membership_id,membership_version,
                 created_at_utc DESC,id DESC)
                WHERE read_at_utc IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $m15_guard$ BEGIN
                IF EXISTS (SELECT 1 FROM m15_notification_outbox)
                    OR EXISTS (SELECT 1 FROM m15_user_notifications)
                    OR EXISTS (SELECT 1 FROM m15_notification_replays) THEN
                    RAISE EXCEPTION 'M15 index downgrade would strand delivery evidence';
                END IF;
            END $m15_guard$;
            DROP INDEX ix_m15_membership_unread_feed;
            DROP INDEX ix_m15_membership_feed;
            """);
    }
}
