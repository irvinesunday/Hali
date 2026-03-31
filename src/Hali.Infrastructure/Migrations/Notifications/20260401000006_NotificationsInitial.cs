using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Notifications
{
    /// <inheritdoc />
    [Migration("20260401000006_NotificationsInitial")]
    public partial class NotificationsInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "follows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_follows", x => x.id);
                    table.UniqueConstraint("UQ_follows_account_locality", x => new { x.account_id, x.locality_id });
                    table.ForeignKey(
                        name: "FK_follows_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_follows_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "varchar(30)", nullable: false),
                    notification_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    send_after = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "queued"),
                    dedupe_key = table.Column<string>(type: "varchar(120)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.UniqueConstraint("UQ_notifications_dedupe_key", x => x.dedupe_key);
                    table.ForeignKey(
                        name: "FK_notifications_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Partial index for notification worker: WHERE status = 'queued' AND send_after <= NOW()
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_notifications_queued_send_after ON notifications(send_after) WHERE status = 'queued';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "notifications");
            migrationBuilder.DropTable(name: "follows");
        }
    }
}
