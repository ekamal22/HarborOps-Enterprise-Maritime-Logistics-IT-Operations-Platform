using HarborOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarborOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HarborOpsDbContext))]
[Migration("20261030170000_InitialBootstrap")]
public sealed class InitialBootstrap : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bootstrap_records",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                dataset_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                seeded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_bootstrap_records", x => x.key));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bootstrap_records");
    }
}
