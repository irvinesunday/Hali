using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations
{
    /// <inheritdoc />
    public partial class Session05Reconcile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:account_type.account_type", "citizen,institution_user,admin")
                .Annotation("Npgsql:Enum:auth_method.auth_method", "phone_otp,email_otp,magic_link,google,apple");

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone_e164 = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_phone_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_fingerprint_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    integrity_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    app_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    expo_push_token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auth_method = table.Column<int>(type: "integer", nullable: false),
                    destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    otp_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_challenges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_email",
                table: "accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_accounts_phone",
                table: "accounts",
                column: "phone_e164",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_account",
                table: "refresh_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_device",
                table: "refresh_tokens",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_tokens_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "otp_challenges");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:account_type.account_type", "citizen,institution_user,admin")
                .OldAnnotation("Npgsql:Enum:auth_method.auth_method", "phone_otp,email_otp,magic_link,google,apple");
        }
    }
}
