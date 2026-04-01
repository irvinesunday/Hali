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
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS civic_category AS ENUM ('roads', 'water', 'electricity', 'transport', 'safety', 'environment', 'governance', 'infrastructure');");
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS location_precision_type AS ENUM ('area', 'road', 'junction', 'landmark', 'facility', 'pin', 'road_landmark');");
		migrationBuilder.Sql("\nCREATE TABLE localities (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    country_code varchar(3) NOT NULL,\n    county_name varchar(100),\n    city_name varchar(100),\n    ward_name varchar(100) NOT NULL,\n    ward_code varchar(50),\n    geom geometry(MultiPolygon, 4326),\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE location_labels (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    locality_id uuid REFERENCES localities(id),\n    area_name varchar(200),\n    road_name varchar(200),\n    junction_description varchar(300),\n    landmark_name varchar(200),\n    facility_name varchar(200),\n    location_label varchar(400) NOT NULL,\n    precision_type location_precision_type NOT NULL,\n    latitude double precision,\n    longitude double precision,\n    geom geometry(Point, 4326),\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE taxonomy_categories (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    category civic_category NOT NULL,\n    subcategory_slug varchar(60) NOT NULL,\n    display_name varchar(120) NOT NULL,\n    description text,\n    is_active boolean NOT NULL DEFAULT true,\n    CONSTRAINT uq_taxonomy_category_slug UNIQUE (category, subcategory_slug)\n);");
		migrationBuilder.Sql("\nCREATE TABLE taxonomy_conditions (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    category civic_category NOT NULL,\n    condition_slug varchar(60) NOT NULL,\n    display_name varchar(120) NOT NULL,\n    ordinal integer NOT NULL DEFAULT 0,\n    is_positive boolean NOT NULL DEFAULT false,\n    CONSTRAINT uq_taxonomy_condition_slug UNIQUE (category, condition_slug)\n);");
		migrationBuilder.Sql("\nCREATE TABLE signal_events (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    account_id uuid,\n    device_id uuid,\n    locality_id uuid REFERENCES localities(id),\n    location_label_id uuid REFERENCES location_labels(id),\n    category civic_category NOT NULL,\n    subcategory_slug varchar(60),\n    condition_slug varchar(60),\n    free_text text,\n    neutral_summary text,\n    temporal_type varchar(30),\n    latitude double precision,\n    longitude double precision,\n    location_confidence numeric(4,3),\n    location_source varchar(20),\n    condition_confidence numeric(4,3),\n    occurred_at timestamptz NOT NULL DEFAULT now(),\n    created_at timestamptz NOT NULL DEFAULT now(),\n    source_language varchar(10),\n    source_channel varchar(20),\n    spatial_cell_id varchar(20),\n    civis_precheck jsonb\n);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_events_locality_category_time ON signal_events(locality_id, category, occurred_at);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_events_spatial_cell_time ON signal_events(spatial_cell_id, occurred_at);");
		migrationBuilder.Sql("CREATE INDEX ix_localities_geom ON localities USING GIST(geom);");
		migrationBuilder.Sql("CREATE INDEX ix_location_labels_geom ON location_labels USING GIST(geom);");
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
