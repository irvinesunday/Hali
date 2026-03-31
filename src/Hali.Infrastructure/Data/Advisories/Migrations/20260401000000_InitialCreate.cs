using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Advisories.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS official_post_type AS ENUM ('live_update', 'scheduled_disruption', 'advisory_public_notice');");

        migrationBuilder.Sql(@"
CREATE TABLE institutions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(200) NOT NULL,
    type varchar(50) NOT NULL,
    jurisdiction_label varchar(200),
    is_verified boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE institution_jurisdictions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id uuid NOT NULL REFERENCES institutions(id),
    locality_id uuid,
    corridor_name varchar(200),
    geom geometry(MultiPolygon, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE official_posts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id uuid NOT NULL REFERENCES institutions(id),
    author_account_id uuid,
    type official_post_type NOT NULL,
    category civic_category NOT NULL,
    title varchar(300) NOT NULL,
    body text NOT NULL,
    starts_at timestamptz,
    ends_at timestamptz,
    status varchar(20) NOT NULL DEFAULT 'draft',
    related_cluster_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE official_post_scopes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    official_post_id uuid NOT NULL REFERENCES official_posts(id),
    locality_id uuid,
    corridor_name varchar(200),
    geom geometry(MultiPolygon, 4326)
);");

        migrationBuilder.Sql("CREATE INDEX ix_institution_jurisdictions_geom ON institution_jurisdictions USING GIST(geom);");
        migrationBuilder.Sql("CREATE INDEX ix_official_post_scopes_geom ON official_post_scopes USING GIST(geom);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS official_post_scopes;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS official_posts;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS institution_jurisdictions;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS institutions;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS official_post_type;");
    }
}
