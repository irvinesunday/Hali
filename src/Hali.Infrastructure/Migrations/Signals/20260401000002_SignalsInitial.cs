using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Signals
{
    /// <inheritdoc />
    [Migration("20260401000002_SignalsInitial")]
    public partial class SignalsInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "localities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    country_code = table.Column<string>(type: "varchar(2)", nullable: false),
                    county_name = table.Column<string>(type: "varchar(120)", nullable: true),
                    city_name = table.Column<string>(type: "varchar(120)", nullable: false),
                    ward_name = table.Column<string>(type: "varchar(120)", nullable: false),
                    ward_code = table.Column<string>(type: "varchar(50)", nullable: true),
                    // geometry(MultiPolygon,4326) — handled via Npgsql.NetTopologySuite
                    geom = table.Column<object>(type: "geometry(MultiPolygon,4326)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_localities", x => x.id);
                });

            // GiST index on localities.geom — required for ST_DWithin proximity queries (schema_patch_notes §12)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_localities_geom ON localities USING GIST(geom);");

            migrationBuilder.CreateTable(
                name: "location_labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    road_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    junction_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    landmark_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    facility_name = table.Column<string>(type: "varchar(160)", nullable: true),
                    location_label = table.Column<string>(type: "varchar(255)", nullable: false),
                    location_precision_type = table.Column<string>(type: "location_precision_type", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    geom = table.Column<object>(type: "geometry(Point,4326)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_labels", x => x.id);
                    table.ForeignKey(
                        name: "FK_location_labels_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // GiST index on location_labels.geom (schema_patch_notes §12)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_location_labels_geom ON location_labels USING GIST(geom);");

            migrationBuilder.CreateTable(
                name: "taxonomy_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category = table.Column<string>(type: "civic_category", nullable: false),
                    subcategory_slug = table.Column<string>(type: "varchar(80)", nullable: false),
                    display_name = table.Column<string>(type: "varchar(120)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxonomy_categories", x => x.id);
                    table.UniqueConstraint("UQ_taxonomy_categories_cat_slug", x => new { x.category, x.subcategory_slug });
                });

            migrationBuilder.CreateTable(
                name: "taxonomy_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category = table.Column<string>(type: "civic_category", nullable: false),
                    condition_slug = table.Column<string>(type: "varchar(80)", nullable: false),
                    display_name = table.Column<string>(type: "varchar(120)", nullable: false),
                    ordinal = table.Column<short>(type: "smallint", nullable: true),
                    is_positive = table.Column<bool>(nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxonomy_conditions", x => x.id);
                    table.UniqueConstraint("UQ_taxonomy_conditions_cat_slug", x => new { x.category, x.condition_slug });
                });

            migrationBuilder.CreateTable(
                name: "signal_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "civic_category", nullable: false),
                    subcategory_slug = table.Column<string>(type: "varchar(80)", nullable: true),
                    condition_slug = table.Column<string>(type: "varchar(80)", nullable: true),
                    free_text = table.Column<string>(type: "text", nullable: true),
                    neutral_summary = table.Column<string>(type: "varchar(240)", nullable: true),
                    temporal_type = table.Column<string>(type: "varchar(40)", nullable: false, defaultValue: "episodic_unknown"),
                    // Direct coordinates per locked decision §5 (schema_patch_notes §8)
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    // NLP location fields per schema_patch_notes §9
                    location_confidence = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    location_source = table.Column<string>(type: "varchar(20)", nullable: true),
                    // NLP condition confidence per schema_patch_notes §11
                    condition_confidence = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    source_language = table.Column<string>(type: "varchar(20)", nullable: true),
                    source_channel = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "app"),
                    spatial_cell_id = table.Column<string>(type: "varchar(80)", nullable: true),
                    civis_precheck = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_signal_events_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_signal_events_devices",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_signal_events_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_signal_events_location_labels",
                        column: x => x.location_label_id,
                        principalTable: "location_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_locality_category_time",
                table: "signal_events",
                columns: new[] { "locality_id", "category", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_spatial_cell_time",
                table: "signal_events",
                columns: new[] { "spatial_cell_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "signal_events");
            migrationBuilder.DropTable(name: "taxonomy_conditions");
            migrationBuilder.DropTable(name: "taxonomy_categories");
            migrationBuilder.DropTable(name: "location_labels");
            migrationBuilder.DropTable(name: "localities");
        }
    }
}
