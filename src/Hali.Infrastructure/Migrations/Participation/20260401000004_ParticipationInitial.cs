using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Participation
{
    /// <inheritdoc />
    [Migration("20260401000004_ParticipationInitial")]
    public partial class ParticipationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    participation_type = table.Column<string>(type: "participation_type", nullable: false),
                    context_text = table.Column<string>(type: "varchar(150)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    idempotency_key = table.Column<string>(type: "varchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participations", x => x.id);
                    // DB-level unique on (cluster_id, device_id, participation_type, idempotency_key).
                    // NOTE: the application layer ALSO enforces one active type per device per cluster
                    // (schema_patch_notes §10) — this constraint does not substitute for that rule.
                    table.UniqueConstraint(
                        "UQ_participations_cluster_device_type_key",
                        x => new { x.cluster_id, x.device_id, x.participation_type, x.idempotency_key });
                    table.ForeignKey(
                        name: "FK_participations_clusters",
                        column: x => x.cluster_id,
                        principalTable: "signal_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participations_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_participations_devices",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_participations_cluster_type_time",
                table: "participations",
                columns: new[] { "cluster_id", "participation_type", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "participations");
        }
    }
}
