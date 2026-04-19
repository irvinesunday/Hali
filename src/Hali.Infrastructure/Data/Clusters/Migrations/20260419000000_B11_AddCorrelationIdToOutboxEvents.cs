using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Clusters.Migrations
{
    /// <inheritdoc />
    public partial class B11_AddCorrelationIdToOutboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // outbox_events is a shared table written from both ClustersDbContext
            // and SignalsDbContext. The parallel Signals migration adds the same
            // columns; whichever migrator runs first wins, and the other becomes
            // a no-op via ADD COLUMN IF NOT EXISTS.
            //
            // Safe pattern for NOT NULL on a non-empty table:
            // 1. Add nullable so existing rows are unaffected.
            // 2. Set the DB default for future inserts.
            // 3. Back-fill existing rows before enforcing NOT NULL.
            // 4. Tighten to NOT NULL.
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ADD COLUMN IF NOT EXISTS correlation_id uuid NULL;");
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ALTER COLUMN correlation_id SET DEFAULT gen_random_uuid();");
            migrationBuilder.Sql(@"
                UPDATE outbox_events
                SET correlation_id = gen_random_uuid()
                WHERE correlation_id IS NULL;");
            migrationBuilder.Sql(@"
                ALTER TABLE outbox_events
                ALTER COLUMN correlation_id SET NOT NULL;");
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
