using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Auth
{
    /// <inheritdoc />
    [Migration("20260401000001_AuthInitial")]
    public partial class AuthInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL extensions
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");

            // Shared enums — created here with idempotent guards and reused by all modules.
            // PostgreSQL enum values cannot be removed once added, so the civic_category enum
            // is defined here with the locked MVP set from schema_patch_notes §7.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE account_type AS ENUM ('citizen','institution_user','admin');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE auth_method AS ENUM ('phone_otp','email_otp','magic_link','google','apple');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE signal_state AS ENUM ('unconfirmed','active','possible_restoration','resolved','expired','suppressed');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE participation_type AS ENUM ('affected','observing','no_longer_affected','restoration_yes','restoration_no','restoration_unsure');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE official_post_type AS ENUM ('live_update','scheduled_disruption','advisory_public_notice');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            // location_precision_type includes road_landmark per schema_patch_notes §4
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE location_precision_type AS ENUM ('area','road','junction','landmark','facility','pin','road_landmark');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            // civic_category: locked MVP set per schema_patch_notes §7
            // Excludes health, education, other. Includes infrastructure.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    CREATE TYPE civic_category AS ENUM ('roads','water','electricity','transport','safety','environment','governance','infrastructure');
                EXCEPTION WHEN duplicate_object THEN NULL; END $$;
                """);

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_type = table.Column<string>(type: "account_type", nullable: false),
                    display_name = table.Column<string>(type: "varchar(80)", nullable: true),
                    email = table.Column<string>(type: "varchar(255)", nullable: true),
                    phone_e164 = table.Column<string>(type: "varchar(30)", nullable: true),
                    is_email_verified = table.Column<bool>(nullable: false, defaultValue: false),
                    is_phone_verified = table.Column<bool>(nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.UniqueConstraint("UQ_accounts_email", x => x.email);
                    table.UniqueConstraint("UQ_accounts_phone_e164", x => x.phone_e164);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_fingerprint_hash = table.Column<string>(type: "varchar(128)", nullable: false),
                    device_integrity_level = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "unknown"),
                    platform = table.Column<string>(type: "varchar(30)", nullable: false),
                    app_version = table.Column<string>(type: "varchar(30)", nullable: true),
                    // expo_push_token added per schema_patch_notes §5
                    expo_push_token = table.Column<string>(type: "varchar(200)", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    is_blocked = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.UniqueConstraint("UQ_devices_fingerprint", x => x.device_fingerprint_hash);
                    table.ForeignKey(
                        name: "FK_devices_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auth_method = table.Column<string>(type: "auth_method", nullable: false),
                    destination = table.Column<string>(type: "varchar(255)", nullable: false),
                    otp_hash = table.Column<string>(type: "varchar(255)", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_otp_challenges_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // refresh_tokens: added per schema_patch_notes §2 and locked decision §4
            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    token_hash = table.Column<string>(type: "varchar(255)", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_devices",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_account_id",
                table: "refresh_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_device_id",
                table: "refresh_tokens",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "refresh_tokens");
            migrationBuilder.DropTable(name: "otp_challenges");
            migrationBuilder.DropTable(name: "devices");
            migrationBuilder.DropTable(name: "accounts");

            migrationBuilder.Sql("DROP TYPE IF EXISTS civic_category;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS location_precision_type;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS official_post_type;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS participation_type;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS signal_state;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS auth_method;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS account_type;");
        }
    }
}
