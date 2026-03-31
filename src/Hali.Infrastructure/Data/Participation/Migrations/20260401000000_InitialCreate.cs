using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Participation.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS participation_type AS ENUM ('affected', 'observing', 'no_longer_affected', 'restoration_yes', 'restoration_no', 'restoration_unsure');");

        migrationBuilder.Sql(@"
CREATE TABLE participations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL,
    account_id uuid,
    device_id uuid,
    participation_type participation_type NOT NULL,
    context_text text,
    created_at timestamptz NOT NULL DEFAULT now(),
    idempotency_key varchar(100),
    CONSTRAINT uq_participation_idempotency UNIQUE (cluster_id, device_id, participation_type, idempotency_key)
);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS participations;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS participation_type;");
    }
}
