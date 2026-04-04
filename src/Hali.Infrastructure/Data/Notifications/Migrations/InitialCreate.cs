using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Notifications.Migrations;

[DbContext(typeof(NotificationsDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("\nCREATE TABLE follows (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_id uuid NOT NULL,\n    locality_id uuid NOT NULL,\n    created_at timestamptz NOT NULL DEFAULT now(),\n    CONSTRAINT uq_follow UNIQUE (account_id, locality_id)\n);");
		migrationBuilder.Sql("\nCREATE TABLE notifications (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_id uuid NOT NULL,\n    channel varchar(20) NOT NULL,\n    notification_type varchar(50) NOT NULL,\n    payload jsonb,\n    send_after timestamptz NOT NULL DEFAULT now(),\n    sent_at timestamptz,\n    status varchar(20) NOT NULL DEFAULT 'queued',\n    dedupe_key varchar(200),\n    CONSTRAINT uq_notification_dedupe UNIQUE (dedupe_key)\n);");
		migrationBuilder.Sql("CREATE INDEX ix_notifications_queued_send_after ON notifications(send_after) WHERE status = 'queued';");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS notifications;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS follows;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
