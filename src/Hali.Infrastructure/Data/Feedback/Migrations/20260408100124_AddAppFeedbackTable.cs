using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Feedback.Migrations
{
    /// <inheritdoc />
    public partial class AddAppFeedbackTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    screen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    app_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_feedback", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_feedback_rating",
                table: "app_feedback",
                column: "rating");

            migrationBuilder.CreateIndex(
                name: "ix_app_feedback_screen",
                table: "app_feedback",
                column: "screen");

            migrationBuilder.CreateIndex(
                name: "ix_app_feedback_submitted_at",
                table: "app_feedback",
                column: "submitted_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_feedback");
        }
    }
}
