using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Advisories.Migrations;

[DbContext(typeof(AdvisoriesDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Enum types only — table creation is handled by AddAdvisoriesSchema migration.
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS official_post_type AS ENUM ('live_update', 'scheduled_disruption', 'advisory_public_notice');");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS official_post_scopes;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS official_posts;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS institution_jurisdictions;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS institutions;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS official_post_type;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
