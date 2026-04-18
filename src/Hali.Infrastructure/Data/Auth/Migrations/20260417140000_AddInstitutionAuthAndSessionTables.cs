using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations;

/// <summary>
/// Phase 2 institution auth + session hardening (#197): introduces four
/// tables that back the web-session lifecycle for institution + admin
/// users. All columns are additive; no existing data is rewritten.
/// </summary>
public partial class AddInstitutionAuthAndSessionTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS web_sessions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES accounts(id),
    institution_id uuid NULL,
    session_token_hash varchar(128) NOT NULL,
    csrf_token_hash varchar(128) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    last_activity_at timestamptz NOT NULL DEFAULT now(),
    absolute_expires_at timestamptz NOT NULL,
    step_up_verified_at timestamptz NULL,
    revoked_at timestamptz NULL,
    CONSTRAINT uq_web_sessions_token UNIQUE (session_token_hash)
);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_web_sessions_account ON web_sessions(account_id);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_web_sessions_absolute_expires ON web_sessions(absolute_expires_at);");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS totp_secrets (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES accounts(id),
    secret_encrypted text NOT NULL,
    enrolled_at timestamptz NOT NULL DEFAULT now(),
    confirmed_at timestamptz NULL,
    revoked_at timestamptz NULL,
    CONSTRAINT uq_totp_secrets_account UNIQUE (account_id)
);");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS totp_recovery_codes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL REFERENCES accounts(id),
    code_hash varchar(128) NOT NULL,
    used_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_totp_recovery_codes UNIQUE (account_id, code_hash)
);");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS magic_link_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    destination_email varchar(254) NOT NULL,
    token_hash varchar(128) NOT NULL,
    account_id uuid NULL REFERENCES accounts(id),
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_magic_link_tokens_hash UNIQUE (token_hash)
);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_magic_link_tokens_email ON magic_link_tokens(destination_email);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_magic_link_tokens_expires ON magic_link_tokens(expires_at);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS magic_link_tokens;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS totp_recovery_codes;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS totp_secrets;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS web_sessions;");
    }
}
