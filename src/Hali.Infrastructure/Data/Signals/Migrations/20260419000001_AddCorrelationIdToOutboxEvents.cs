using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Signals.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationIdToOutboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // outbox_events is a shared table written from both SignalsDbContext
            // and ClustersDbContext. The parallel Clusters migration
            // (20260419000000_B11_AddCorrelationIdToOutboxEvents) adds the same
            // columns; whichever migrator runs first wins, and the other becomes
            // a no-op via ADD COLUMN IF NOT EXISTS.
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ADD COLUMN IF NOT EXISTS correlation_id uuid NOT NULL DEFAULT gen_random_uuid();");
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ADD COLUMN IF NOT EXISTS causation_id uuid NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events DROP COLUMN IF EXISTS causation_id;");
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events DROP COLUMN IF EXISTS correlation_id;");
        }
    }
}
