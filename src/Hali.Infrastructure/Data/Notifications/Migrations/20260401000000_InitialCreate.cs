using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Notifications.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE follows (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL,
    locality_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_follow UNIQUE (account_id, locality_id)
);");

        migrationBuilder.Sql(@"
CREATE TABLE notifications (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL,
    channel varchar(20) NOT NULL,
    notification_type varchar(50) NOT NULL,
    payload jsonb,
    send_after timestamptz NOT NULL DEFAULT now(),
    sent_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'queued',
    dedupe_key varchar(200),
    CONSTRAINT uq_notification_dedupe UNIQUE (dedupe_key)
);");

        migrationBuilder.Sql("CREATE INDEX ix_notifications_queued_send_after ON notifications(send_after) WHERE status = 'queued';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS notifications;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS follows;");
    }
}
