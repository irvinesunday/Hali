using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations
{
    /// <summary>
    /// Reconciles the EF model snapshot with the actual DbContext configuration.
    /// The database schema is already correct (created by InitialCreate raw SQL).
    /// This migration exists only to update the snapshot so EF stops reporting
    /// pending model changes.
    /// </summary>
    public partial class ReconcileAuthModelSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: the database schema is already correct.
            // Column names (device_integrity_level, first_seen_at), max lengths (128, 30),
            // and enum types (account_type, auth_method) were set correctly by
            // InitialCreate's raw SQL. Only the EF snapshot was out of sync.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: nothing to reverse.
        }
    }
}
