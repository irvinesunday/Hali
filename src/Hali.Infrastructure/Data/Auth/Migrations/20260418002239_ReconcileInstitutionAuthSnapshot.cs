using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileInstitutionAuthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No DDL: web_sessions, totp_secrets, totp_recovery_codes, magic_link_tokens,
            // and accounts.is_institution_admin were all created by the preceding raw-SQL
            // migrations (20260417140000 / 20260417170000) using IF NOT EXISTS / IF NOT EXISTS.
            // This migration exists solely to advance the EF model snapshot so that
            // dotnet-ef no longer reports PendingModelChangesWarning.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback is handled by the raw-SQL migrations above this one.
        }
    }
}
