using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"pgcrypto\";");
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");

        migrationBuilder.Sql("CREATE TYPE account_type AS ENUM ('citizen', 'institution_user', 'admin');");
        migrationBuilder.Sql("CREATE TYPE auth_method AS ENUM ('phone_otp', 'email_otp', 'magic_link', 'google', 'apple');");

        migrationBuilder.Sql(@"
CREATE TABLE accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_type account_type NOT NULL DEFAULT 'citizen',
    display_name varchar(120),
    email varchar(254),
    phone_e164 varchar(20),
    is_phone_verified boolean NOT NULL DEFAULT false,
    is_email_verified boolean NOT NULL DEFAULT false,
    status varchar(20) NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_accounts_email UNIQUE (email),
    CONSTRAINT uq_accounts_phone UNIQUE (phone_e164)
);");

        migrationBuilder.Sql(@"
CREATE TABLE devices (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid REFERENCES accounts(id),
    device_fingerprint_hash varchar(64) NOT NULL,
    integrity_level varchar(20) NOT NULL DEFAULT 'unknown',
    platform varchar(20),
    app_version varchar(20),
    expo_push_token varchar(200),
    created_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    is_blocked boolean NOT NULL DEFAULT false
);");

        migrationBuilder.Sql(@"
CREATE TABLE otp_challenges (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid REFERENCES accounts(id),
    auth_method auth_method NOT NULL,
    destination varchar(254) NOT NULL,
    otp_hash varchar(128) NOT NULL,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    token_hash varchar(128) NOT NULL,
    account_id uuid NOT NULL REFERENCES accounts(id),
    device_id uuid REFERENCES devices(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz,
    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)
);");

        migrationBuilder.Sql("CREATE UNIQUE INDEX ix_refresh_tokens_hash ON refresh_tokens(token_hash);");
        migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_account ON refresh_tokens(account_id);");
        migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_device ON refresh_tokens(device_id);");
        migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_expires ON refresh_tokens(expires_at);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS refresh_tokens;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS otp_challenges;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS devices;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS accounts;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS auth_method;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS account_type;");
    }
}
