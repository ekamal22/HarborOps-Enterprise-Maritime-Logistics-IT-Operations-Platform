import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

// Applies one disposable G9 mutation to an isolated source export. Every edit must match its
// anchor exactly once, so a mutation can never silently apply to the wrong place or twice.
const worker = "src/HarborOps.Infrastructure/Integrations/TrackingWorkStore.cs";
const inbox = "src/HarborOps.Infrastructure/Integrations/TrackingInboxStore.cs";
const migration = "src/HarborOps.Infrastructure/Persistence/Migrations/20261104190000_TrackingWorker.cs";
const policy = "src/HarborOps.Domain/Organizations/OrganizationPolicy.cs";

const heartbeatCredential = "AND lease_token={claim.LeaseToken} AND lease_epoch={claim.LeaseEpoch}";
const finalizeSelect = "              AND state='Leased' AND lease_token=@token AND lease_epoch=@epoch\n" +
    "              AND redrive_generation=@generation AND lease_expires_at_utc>clock_timestamp()\n" +
    "            FOR UPDATE";
const finalizeUpdate = "              AND state='Leased' AND lease_token=@token AND lease_epoch=@epoch\n" +
    "              AND redrive_generation=@generation AND lease_expires_at_utc>clock_timestamp()\n" +
    "            \"\"\", ct, (\"state\", target)";

export const mutations = {
    "effect-unique": [{ path: migration,
        from: "CREATE TABLE tracking_processing_effects (\n                receipt_id uuid PRIMARY KEY,",
        to: "CREATE TABLE tracking_processing_effects (\n                receipt_id uuid NOT NULL," }],
    "lease-token": [
        { path: worker, from: heartbeatCredential, to: "AND lease_epoch={claim.LeaseEpoch}" },
        { path: worker, from: finalizeSelect, to: finalizeSelect.replace("lease_token=@token AND ", "") },
        { path: worker, from: finalizeUpdate, to: finalizeUpdate.replace("lease_token=@token AND ", "") }],
    "lease-epoch": [
        { path: worker, from: heartbeatCredential, to: "AND lease_token={claim.LeaseToken}" }],
    "lease-expiry": [
        { path: worker,
          from: "              AND lease_expires_at_utc>clock_timestamp()\n              AND EXISTS (SELECT 1 FROM organizations o",
          to: "              AND TRUE\n              AND EXISTS (SELECT 1 FROM organizations o" },
        { path: worker, from: finalizeSelect, to: finalizeSelect.replace(" AND lease_expires_at_utc>clock_timestamp()", "") }],
    "claim-lock": [{ path: worker,
        from: "        if (!await LockActiveOrganizationAsync(connection, tx, organizationId, ct))\n            return null;\n\n        Guid receipt = default, source = default;",
        to: "        Guid receipt = default, source = default;" }],
    "capacity-lock": [{ path: inbox,
        from: "\"SELECT status FROM organizations WHERE id = @organization FOR UPDATE\"",
        to: "\"SELECT status FROM organizations WHERE id = @organization\"" }],
    "unresolved-count": [{ path: inbox,
        from: "AND state IN ('Pending','Leased','RetryWaiting','NeedsReconciliation')",
        to: "AND state IN ('Pending')" }],
    "retry-budget": [{ path: worker,
        from: "var exhausted = !retryable || claim.Attempt >= settings.MaxAttempts;",
        to: "var exhausted = !retryable || claim.Attempt >= settings.MaxAttempts + 100;" }],
    "redrive-auth": [{ path: policy,
        from: "(permission == OrganizationPermission.TrackingWorkRedrive &&\n            role == OrganizationRole.Manager) ||",
        to: "(permission == OrganizationPermission.TrackingWorkRedrive &&\n            role is (OrganizationRole.Manager or OrganizationRole.Analyst)) ||" }],
    "tenant-filter": [{ path: worker,
        from: "            WHERE c.organization_id=@organization\n              AND c.container_identifier=@equipment",
        to: "            WHERE c.container_identifier=@equipment" }],
    "transition-guard": [{ path: migration,
        from: "            CREATE TRIGGER m9_work_guard BEFORE INSERT OR UPDATE OR DELETE\n                ON tracking_inbox_work_items FOR EACH ROW\n                EXECUTE FUNCTION m9_guard_work_item();",
        to: "            -- Disposable mutation: work transition guard trigger omitted." }],
    "commit-ack": [{ path: worker,
        from: "        await transaction.CommitAsync(ct);\n        return resolved.HasValue ? TrackingWorkDisposition.Processed :",
        to: "        // Disposable mutation: acknowledge without COMMIT.\n        return resolved.HasValue ? TrackingWorkDisposition.Processed :" }]
};

if (import.meta.url === new URL(process.argv[1], "file:///").href || process.argv[1]?.endsWith("apply-g9-mutation.mjs")) {
    const [variant, root] = process.argv.slice(2);
    assert.ok(variant && root && fs.existsSync(root), "Mutation variant and isolated export required");
    const edits = mutations[variant];
    assert.ok(edits, "Unsupported G9 mutation: " + variant);
    for (const edit of edits) {
        const file = path.join(root, edit.path);
        const source = fs.readFileSync(file, "utf8");
        assert.equal(source.split(edit.from).length - 1, 1, "Mutation anchor missing or ambiguous: " + variant);
        assert.notEqual(edit.from, edit.to, "Mutation would be a no-op: " + variant);
        fs.writeFileSync(file, source.replace(edit.from, () => edit.to));
    }
    console.log("Applied disposable G9 mutation: " + variant + " (" + edits.length + " edits)");
}
