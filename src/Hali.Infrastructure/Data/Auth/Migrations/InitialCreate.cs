using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"pgcrypto\";");
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");
		migrationBuilder.Sql("CREATE TYPE account_type AS ENUM ('citizen', 'institution_user', 'admin');");
		migrationBuilder.Sql("CREATE TYPE auth_method AS ENUM ('phone_otp', 'email_otp', 'magic_link', 'google', 'apple');");
		migrationBuilder.Sql("\nCREATE TABLE accounts (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_type account_type NOT NULL DEFAULT 'citizen',\n    display_name varchar(120),\n    email varchar(254),\n    phone_e164 varchar(20),\n    is_phone_verified boolean NOT NULL DEFAULT false,\n    is_email_verified boolean NOT NULL DEFAULT false,\n    status varchar(20) NOT NULL DEFAULT 'active',\n    created_at timestamptz NOT NULL DEFAULT now(),\n    updated_at timestamptz NOT NULL DEFAULT now(),\n    CONSTRAINT uq_accounts_email UNIQUE (email),\n    CONSTRAINT uq_accounts_phone UNIQUE (phone_e164)\n);");
		migrationBuilder.Sql("\nCREATE TABLE devices (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_id uuid REFERENCES accounts(id),\n    device_fingerprint_hash varchar(64) NOT NULL,\n    integrity_level varchar(20) NOT NULL DEFAULT 'unknown',\n    platform varchar(20),\n    app_version varchar(20),\n    expo_push_token varchar(200),\n    created_at timestamptz NOT NULL DEFAULT now(),\n    last_seen_at timestamptz NOT NULL DEFAULT now(),\n    is_blocked boolean NOT NULL DEFAULT false\n);");
		migrationBuilder.Sql("\nCREATE TABLE otp_challenges (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_id uuid REFERENCES accounts(id),\n    auth_method auth_method NOT NULL,\n    destination varchar(254) NOT NULL,\n    otp_hash varchar(128) NOT NULL,\n    expires_at timestamptz NOT NULL,\n    consumed_at timestamptz,\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE refresh_tokens (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    token_hash varchar(128) NOT NULL,\n    account_id uuid NOT NULL REFERENCES accounts(id),\n    device_id uuid REFERENCES devices(id),\n    created_at timestamptz NOT NULL DEFAULT now(),\n    expires_at timestamptz NOT NULL,\n    revoked_at timestamptz,\n    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash)\n);");
		migrationBuilder.Sql("CREATE UNIQUE INDEX ix_refresh_tokens_hash ON refresh_tokens(token_hash);");
		migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_account ON refresh_tokens(account_id);");
		migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_device ON refresh_tokens(device_id);");
		migrationBuilder.Sql("CREATE INDEX ix_refresh_tokens_expires ON refresh_tokens(expires_at);");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS refresh_tokens;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS otp_challenges;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS devices;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS accounts;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS auth_method;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS account_type;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
