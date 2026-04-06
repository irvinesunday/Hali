using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AuthDbContext))]
[Migration("20260403000000_B5InstitutionAuth")]
public class B5InstitutionAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: columns and table are created by SyncEnumTypeAnnotations migration.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS institution_invites;");
        migrationBuilder.Sql("ALTER TABLE accounts DROP COLUMN IF EXISTS institution_id;");
        migrationBuilder.Sql("ALTER TABLE accounts DROP COLUMN IF EXISTS is_blocked;");
    }
}
