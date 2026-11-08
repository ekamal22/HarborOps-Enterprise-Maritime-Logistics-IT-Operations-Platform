using HarborOps.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace HarborOps.UnitTests;

public sealed class M12MigrationSafetyTests
{
    [Fact]
    public void DowngradeRefusesToRemoveAuditAuthorityAfterAnExport()
    {
        var migration = new M12ShipmentExportAudit();
        var operations = migration.DownOperations.OfType<SqlOperation>().ToArray();
        Assert.Single(operations);
        var sql = operations[0].Sql;
        Assert.Contains("event_type = 'shipment.export_prepared'", sql, StringComparison.Ordinal);
        Assert.Contains("ERRCODE = '23514'", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf("RAISE EXCEPTION", StringComparison.Ordinal) <
            sql.IndexOf("DROP FUNCTION", StringComparison.Ordinal),
            "The audit-history guard must execute before any destructive downgrade operation");
    }

    [Fact]
    public void UpgradeRestrictsVerifierAndRequiresSignedNonreplayableAudit()
    {
        var migration = new M12ShipmentExportAudit();
        var statements = string.Join("\n",
            migration.UpOperations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("CREATE TABLE m12_export_proof_keys", statements,
            StringComparison.Ordinal);
        Assert.Contains("REVOKE ALL ON TABLE m12_export_proof_keys FROM PUBLIC",
            statements, StringComparison.Ordinal);
        Assert.Contains("public.hmac(", statements, StringComparison.Ordinal);
        Assert.Contains("DECLARE target_schema text := current_schema()", statements,
            StringComparison.Ordinal);
        Assert.Contains("SET search_path TO pg_catalog,%1$I,public", statements,
            StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX uq_m12_export_prepared_correlation",
            statements, StringComparison.Ordinal);
        Assert.Contains("REVOKE ALL ON FUNCTION m12_record_shipment_export(",
            statements, StringComparison.Ordinal);
    }
}
