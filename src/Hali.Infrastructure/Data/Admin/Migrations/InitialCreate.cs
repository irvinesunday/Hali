using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Admin.Migrations;

[DbContext(typeof(AdminDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("\nCREATE TABLE admin_audit_logs (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    actor_account_id uuid,\n    action varchar(100) NOT NULL,\n    target_type varchar(50),\n    target_id uuid,\n    metadata jsonb,\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS admin_audit_logs;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
