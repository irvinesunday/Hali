using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Advisories.Migrations;

/// <inheritdoc />
public partial class AddOfficialPostResponseStatusAndSeverity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Additive, nullable columns for Phase 2 institution operational routes.
        //   response_status  — canonical ResponseStatus vocabulary for live_update posts
        //                      (acknowledged, teams_dispatched, teams_on_site, work_ongoing,
        //                       restoration_in_progress, service_restored). Null otherwise.
        //   severity         — optional severity for scheduled_disruption posts
        //                      (minor, moderate, major). Null otherwise.
        // Idempotent ADD COLUMN IF NOT EXISTS so this migration is safe to
        // rerun against environments where the column may already exist
        // (e.g. re-applied integration test schemas).
        migrationBuilder.Sql(
            "ALTER TABLE official_posts ADD COLUMN IF NOT EXISTS response_status character varying(50) NULL;");
        migrationBuilder.Sql(
            "ALTER TABLE official_posts ADD COLUMN IF NOT EXISTS severity character varying(20) NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE official_posts DROP COLUMN IF EXISTS severity;");
        migrationBuilder.Sql("ALTER TABLE official_posts DROP COLUMN IF EXISTS response_status;");
    }
}
