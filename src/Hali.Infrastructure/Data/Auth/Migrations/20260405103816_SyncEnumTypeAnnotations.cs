using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Auth.Migrations
{
    /// <inheritdoc />
    public partial class SyncEnumTypeAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "institution_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    invited_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institution_invites", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_institution_invites_institution",
                table: "institution_invites",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "ix_institution_invites_token",
                table: "institution_invites",
                column: "invite_token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institution_invites");

            migrationBuilder.DropColumn(
                name: "institution_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_blocked",
                table: "accounts");
        }
    }
}
