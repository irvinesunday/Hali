using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Clusters.Migrations;

[DbContext(typeof(ClustersDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Enum types only — table creation is handled by AddClustersSchema migration.
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS signal_state AS ENUM ('unconfirmed', 'active', 'possible_restoration', 'resolved', 'expired', 'suppressed');");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS civis_decisions;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS cluster_event_links;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS signal_clusters;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS signal_state;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
