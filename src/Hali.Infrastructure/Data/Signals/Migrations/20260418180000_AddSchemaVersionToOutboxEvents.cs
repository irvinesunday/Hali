using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Signals.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaVersionToOutboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // outbox_events is a shared table written from both SignalsDbContext
            // and ClustersDbContext. The parallel Clusters migration
            // (20260418153242_B10_AddSchemaVersionToOutboxEvents) adds the same
            // column; whichever context's migrator runs first wins, and the
            // other becomes a no-op via ADD COLUMN IF NOT EXISTS. Kept in both
            // migration sets so a clean apply of either context's migrations
            // produces a runnable schema.
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ADD COLUMN IF NOT EXISTS schema_version character varying(20) NOT NULL DEFAULT '1.0';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events DROP COLUMN IF EXISTS schema_version;");
        }
    }
}
