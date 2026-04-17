using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations;

/// <summary>
/// Phase 2 institution-admin routes (#196). Adds two columns:
///   * accounts.is_institution_admin — per-user admin flag for an
///     institution_user account. Distinct from the platform-level
///     AccountType.Admin (Hali-ops) — an institution_user can carry
///     this flag to manage their own institution's user list without
///     the cross-institution reach Hali-ops has.
///   * web_sessions.role — role snapshotted at session creation so the
///     session middleware can emit the role claim without a per-request
///     Account lookup. Role values: institution, institution_admin.
///     A change to the account's admin flag takes effect on the next
///     login (session issue) — a live session retains the snapshotted
///     role until it expires / is revoked, which bounds privilege
///     escalation / de-escalation to the 12h absolute window.
/// </summary>
public partial class AddInstitutionAdminRoleColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts ADD COLUMN IF NOT EXISTS is_institution_admin boolean NOT NULL DEFAULT false;");

        migrationBuilder.Sql(
            "ALTER TABLE web_sessions ADD COLUMN IF NOT EXISTS role varchar(30) NOT NULL DEFAULT 'institution';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE web_sessions DROP COLUMN IF EXISTS role;");
        migrationBuilder.Sql("ALTER TABLE accounts DROP COLUMN IF EXISTS is_institution_admin;");
    }
}
