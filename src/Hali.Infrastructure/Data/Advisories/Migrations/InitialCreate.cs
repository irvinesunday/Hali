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
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS official_post_type AS ENUM ('live_update', 'scheduled_disruption', 'advisory_public_notice');");
		migrationBuilder.Sql("\nCREATE TABLE institutions (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    name varchar(200) NOT NULL,\n    type varchar(50) NOT NULL,\n    jurisdiction_label varchar(200),\n    is_verified boolean NOT NULL DEFAULT false,\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE institution_jurisdictions (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    institution_id uuid NOT NULL REFERENCES institutions(id),\n    locality_id uuid,\n    corridor_name varchar(200),\n    geom geometry(MultiPolygon, 4326),\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE official_posts (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    institution_id uuid NOT NULL REFERENCES institutions(id),\n    author_account_id uuid,\n    type official_post_type NOT NULL,\n    category civic_category NOT NULL,\n    title varchar(300) NOT NULL,\n    body text NOT NULL,\n    starts_at timestamptz,\n    ends_at timestamptz,\n    status varchar(20) NOT NULL DEFAULT 'draft',\n    related_cluster_id uuid,\n    created_at timestamptz NOT NULL DEFAULT now(),\n    updated_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE official_post_scopes (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    official_post_id uuid NOT NULL REFERENCES official_posts(id),\n    locality_id uuid,\n    corridor_name varchar(200),\n    geom geometry(MultiPolygon, 4326)\n);");
		migrationBuilder.Sql("CREATE INDEX ix_institution_jurisdictions_geom ON institution_jurisdictions USING GIST(geom);");
		migrationBuilder.Sql("CREATE INDEX ix_official_post_scopes_geom ON official_post_scopes USING GIST(geom);");
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
