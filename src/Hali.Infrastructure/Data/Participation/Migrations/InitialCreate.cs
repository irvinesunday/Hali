using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Participation.Migrations;

[DbContext(typeof(ParticipationDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS participation_type AS ENUM ('affected', 'observing', 'no_longer_affected', 'restoration_yes', 'restoration_no', 'restoration_unsure');");
		migrationBuilder.Sql("\nCREATE TABLE participations (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    cluster_id uuid NOT NULL,\n    account_id uuid,\n    device_id uuid,\n    participation_type participation_type NOT NULL,\n    context_text text,\n    created_at timestamptz NOT NULL DEFAULT now(),\n    idempotency_key varchar(100),\n    CONSTRAINT uq_participation_idempotency UNIQUE (cluster_id, device_id, participation_type, idempotency_key)\n);");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS participations;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS participation_type;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
