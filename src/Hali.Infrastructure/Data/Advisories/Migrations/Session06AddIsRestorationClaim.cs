using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Advisories.Migrations;

[DbContext(typeof(AdvisoriesDbContext))]
[Migration("20260401120000_Session06AddIsRestorationClaim")]
public class Session06AddIsRestorationClaim : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: is_restoration_claim is included in AddAdvisoriesSchema table creation.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE official_posts DROP COLUMN IF EXISTS is_restoration_claim;");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
        modelBuilder.UseIdentityByDefaultColumns();
    }
}
