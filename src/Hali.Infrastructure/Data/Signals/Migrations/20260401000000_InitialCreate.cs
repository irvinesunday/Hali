using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Signals.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS civic_category AS ENUM ('roads', 'water', 'electricity', 'transport', 'safety', 'environment', 'governance', 'infrastructure');");
        migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS location_precision_type AS ENUM ('area', 'road', 'junction', 'landmark', 'facility', 'pin', 'road_landmark');");

        migrationBuilder.Sql(@"
CREATE TABLE localities (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    country_code varchar(3) NOT NULL,
    county_name varchar(100),
    city_name varchar(100),
    ward_name varchar(100) NOT NULL,
    ward_code varchar(50),
    geom geometry(MultiPolygon, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE location_labels (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locality_id uuid REFERENCES localities(id),
    area_name varchar(200),
    road_name varchar(200),
    junction_description varchar(300),
    landmark_name varchar(200),
    facility_name varchar(200),
    location_label varchar(400) NOT NULL,
    precision_type location_precision_type NOT NULL,
    latitude double precision,
    longitude double precision,
    geom geometry(Point, 4326),
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE taxonomy_categories (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category civic_category NOT NULL,
    subcategory_slug varchar(60) NOT NULL,
    display_name varchar(120) NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT uq_taxonomy_category_slug UNIQUE (category, subcategory_slug)
);");

        migrationBuilder.Sql(@"
CREATE TABLE taxonomy_conditions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    category civic_category NOT NULL,
    condition_slug varchar(60) NOT NULL,
    display_name varchar(120) NOT NULL,
    ordinal integer NOT NULL DEFAULT 0,
    is_positive boolean NOT NULL DEFAULT false,
    CONSTRAINT uq_taxonomy_condition_slug UNIQUE (category, condition_slug)
);");

        migrationBuilder.Sql(@"
CREATE TABLE signal_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid,
    device_id uuid,
    locality_id uuid REFERENCES localities(id),
    location_label_id uuid REFERENCES location_labels(id),
    category civic_category NOT NULL,
    subcategory_slug varchar(60),
    condition_slug varchar(60),
    free_text text,
    neutral_summary text,
    temporal_type varchar(30),
    latitude double precision,
    longitude double precision,
    location_confidence numeric(4,3),
    location_source varchar(20),
    condition_confidence numeric(4,3),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    created_at timestamptz NOT NULL DEFAULT now(),
    source_language varchar(10),
    source_channel varchar(20),
    spatial_cell_id varchar(20),
    civis_precheck jsonb
);");

        migrationBuilder.Sql("CREATE INDEX ix_signal_events_locality_category_time ON signal_events(locality_id, category, occurred_at);");
        migrationBuilder.Sql("CREATE INDEX ix_signal_events_spatial_cell_time ON signal_events(spatial_cell_id, occurred_at);");
        migrationBuilder.Sql("CREATE INDEX ix_localities_geom ON localities USING GIST(geom);");
        migrationBuilder.Sql("CREATE INDEX ix_location_labels_geom ON location_labels USING GIST(geom);");
    }

    /// <inheritdoc />
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
}
