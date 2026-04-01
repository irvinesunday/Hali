using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Participation.Migrations
{
    /// <inheritdoc />
    public partial class Session05Reconcile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");

            migrationBuilder.CreateTable(
                name: "participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    participation_type = table.Column<int>(type: "integer", nullable: false),
                    context_text = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_participation_idempotency",
                table: "participations",
                columns: new[] { "cluster_id", "device_id", "participation_type", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");
        }
    }
}
