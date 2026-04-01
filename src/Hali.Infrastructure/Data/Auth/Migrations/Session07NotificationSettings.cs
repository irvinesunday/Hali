using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AuthDbContext))]
[Migration("20260401160000_Session07NotificationSettings")]
public class Session07NotificationSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts ADD COLUMN IF NOT EXISTS notification_settings jsonb;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts DROP COLUMN IF EXISTS notification_settings;");
    }
}
