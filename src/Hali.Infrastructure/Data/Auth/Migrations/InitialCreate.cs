using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Extensions and enum types only — table creation is handled by Session05Reconcile.
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"pgcrypto\";");
		migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");
		migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'account_type' AND typnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public')) THEN CREATE TYPE account_type AS ENUM ('citizen', 'institution_user', 'admin'); END IF; END $$;");
		migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'auth_method' AND typnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public')) THEN CREATE TYPE auth_method AS ENUM ('phone_otp', 'email_otp', 'magic_link', 'google', 'apple'); END IF; END $$;");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS refresh_tokens;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS otp_challenges;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS devices;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS accounts;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS auth_method;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS account_type;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
