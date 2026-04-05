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
		// Enum types only — table creation is handled by Session05Reconcile migration.
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS participation_type AS ENUM ('affected', 'observing', 'no_longer_affected', 'restoration_yes', 'restoration_no', 'restoration_unsure');");
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
