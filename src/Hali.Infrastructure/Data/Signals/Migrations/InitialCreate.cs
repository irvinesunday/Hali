using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Signals.Migrations;

[DbContext(typeof(SignalsDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Enum types only — table creation is handled by AddSignalsSchema migration.
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS civic_category AS ENUM ('roads', 'water', 'electricity', 'transport', 'safety', 'environment', 'governance', 'infrastructure');");
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS location_precision_type AS ENUM ('area', 'road', 'junction', 'landmark', 'facility', 'pin', 'road_landmark');");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS signal_events;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS taxonomy_conditions;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS taxonomy_categories;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS location_labels;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS localities;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS location_precision_type;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS civic_category;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
