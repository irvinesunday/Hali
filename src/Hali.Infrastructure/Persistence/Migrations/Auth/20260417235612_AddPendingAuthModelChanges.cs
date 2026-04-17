using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Persistence.Migrations.Auth
{
    /// <inheritdoc />
    public partial class AddPendingAuthModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_institution_admin",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "magic_link_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_magic_link_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "totp_recovery_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_totp_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "totp_secrets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secret_encrypted = table.Column<string>(type: "text", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_totp_secrets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "web_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    csrf_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    step_up_verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_web_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_email",
                table: "magic_link_tokens",
                column: "destination_email");

            migrationBuilder.CreateIndex(
                name: "ix_magic_link_tokens_expires",
                table: "magic_link_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "uq_magic_link_tokens_hash",
                table: "magic_link_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_totp_recovery_codes",
                table: "totp_recovery_codes",
                columns: new[] { "account_id", "code_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_totp_secrets_account",
                table: "totp_secrets",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_web_sessions_absolute_expires",
                table: "web_sessions",
                column: "absolute_expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_web_sessions_account",
                table: "web_sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "uq_web_sessions_token",
                table: "web_sessions",
                column: "session_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "magic_link_tokens");

            migrationBuilder.DropTable(
                name: "totp_recovery_codes");

            migrationBuilder.DropTable(
                name: "totp_secrets");

            migrationBuilder.DropTable(
                name: "web_sessions");

            migrationBuilder.DropColumn(
                name: "is_institution_admin",
                table: "accounts");
        }
    }
}
