using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hali.Infrastructure.Data.Signals.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .Annotation("Npgsql:Enum:location_precision_type.location_precision_type", "area,road,junction,landmark,facility,pin,road_landmark")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "localities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    county_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ward_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ward_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    geom = table.Column<MultiPolygon>(type: "geometry(MultiPolygon, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location_labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    road_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    junction_description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    landmark_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    facility_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    location_label = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    precision_type = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    geom = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_labels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signal_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<int>(type: "integer", nullable: false),
                    subcategory_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    condition_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    free_text = table.Column<string>(type: "text", nullable: true),
                    neutral_summary = table.Column<string>(type: "text", nullable: true),
                    temporal_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    location_confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    location_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    condition_confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    spatial_cell_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    civis_precheck = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "taxonomy_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    subcategory_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxonomy_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "taxonomy_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    condition_slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    is_positive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxonomy_conditions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_locality_category_time",
                table: "signal_events",
                columns: new[] { "locality_id", "category", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_spatial_cell_time",
                table: "signal_events",
                columns: new[] { "spatial_cell_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "uq_taxonomy_category_slug",
                table: "taxonomy_categories",
                columns: new[] { "category", "subcategory_slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_taxonomy_condition_slug",
                table: "taxonomy_conditions",
                columns: new[] { "category", "condition_slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "localities");

            migrationBuilder.DropTable(
                name: "location_labels");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "signal_events");

            migrationBuilder.DropTable(
                name: "taxonomy_categories");

            migrationBuilder.DropTable(
                name: "taxonomy_conditions");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .OldAnnotation("Npgsql:Enum:location_precision_type.location_precision_type", "area,road,junction,landmark,facility,pin,road_landmark")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
