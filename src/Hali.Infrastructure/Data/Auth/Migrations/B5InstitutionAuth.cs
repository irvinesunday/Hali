using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AuthDbContext))]
[Migration("20260403000000_B5InstitutionAuth")]
public class B5InstitutionAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts ADD COLUMN IF NOT EXISTS institution_id UUID NULL;");

        migrationBuilder.Sql(
            "ALTER TABLE accounts ADD COLUMN IF NOT EXISTS is_blocked BOOLEAN NOT NULL DEFAULT false;");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS institution_invites (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id UUID NOT NULL,
    invite_token_hash VARCHAR(64) NOT NULL,
    invited_by_account_id UUID NOT NULL REFERENCES accounts(id),
    expires_at TIMESTAMPTZ NOT NULL,
    accepted_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_institution_invites_token UNIQUE (invite_token_hash)
);");

        migrationBuilder.Sql(
            "CREATE INDEX IF NOT EXISTS ix_institution_invites_token ON institution_invites(invite_token_hash);");

        migrationBuilder.Sql(
            "CREATE INDEX IF NOT EXISTS ix_institution_invites_institution ON institution_invites(institution_id);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS institution_invites;");
        migrationBuilder.Sql("ALTER TABLE accounts DROP COLUMN IF EXISTS institution_id;");
        migrationBuilder.Sql("ALTER TABLE accounts DROP COLUMN IF EXISTS is_blocked;");
    }
}
