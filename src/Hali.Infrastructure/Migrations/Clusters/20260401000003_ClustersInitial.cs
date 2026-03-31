using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Migrations.Clusters
{
    /// <inheritdoc />
    [Migration("20260401000003_ClustersInitial")]
    public partial class ClustersInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signal_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "civic_category", nullable: false),
                    subcategory_slug = table.Column<string>(type: "varchar(80)", nullable: true),
                    dominant_condition_slug = table.Column<string>(type: "varchar(80)", nullable: true),
                    state = table.Column<string>(type: "signal_state", nullable: false, defaultValue: "unconfirmed"),
                    title = table.Column<string>(type: "varchar(240)", nullable: false),
                    summary = table.Column<string>(type: "varchar(280)", nullable: false),
                    location_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    // geometry(Point,4326) for centroid — handled via Npgsql.NetTopologySuite
                    centroid = table.Column<object>(type: "geometry(Point,4326)", nullable: true),
                    spatial_cell_id = table.Column<string>(type: "varchar(80)", nullable: true),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    possible_restoration_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    civis_score = table.Column<decimal>(type: "numeric(8,4)", nullable: true),
                    wrab = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    sds = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    macf = table.Column<int>(type: "integer", nullable: true),
                    raw_confirmation_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    // temporal_type drives home feed section routing (Active now / Recurring / Other)
                    temporal_type = table.Column<string>(type: "varchar(40)", nullable: false, defaultValue: "episodic_unknown"),
                    // Denormalised participation counts — maintained by clustering worker
                    affected_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    observing_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                    // NOTE: no unique constraint on title — deduplication via join-score threshold (schema_patch_notes §6)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_clusters", x => x.id);
                    table.ForeignKey(
                        name: "FK_signal_clusters_localities",
                        column: x => x.locality_id,
                        principalTable: "localities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_signal_clusters_location_labels",
                        column: x => x.location_label_id,
                        principalTable: "location_labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_state_locality_category",
                table: "signal_clusters",
                columns: new[] { "state", "locality_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_last_seen",
                table: "signal_clusters",
                column: "last_seen_at");

            // Covering index replacing unique constraint (schema_patch_notes §6)
            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_spatial_cell_category",
                table: "signal_clusters",
                columns: new[] { "spatial_cell_id", "category" });

            // GiST index on signal_clusters.centroid — required for ST_DWithin (schema_patch_notes §12)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_signal_clusters_centroid ON signal_clusters USING GIST(centroid);");

            migrationBuilder.CreateTable(
                name: "cluster_event_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_reason = table.Column<string>(type: "varchar(50)", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_event_links", x => x.id);
                    table.UniqueConstraint("UQ_cluster_event_links", x => new { x.cluster_id, x.signal_event_id });
                    table.ForeignKey(
                        name: "FK_cluster_event_links_clusters",
                        column: x => x.cluster_id,
                        principalTable: "signal_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cluster_event_links_signal_events",
                        column: x => x.signal_event_id,
                        principalTable: "signal_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "civis_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    reason_codes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    metrics = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_civis_decisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_civis_decisions_clusters",
                        column: x => x.cluster_id,
                        principalTable: "signal_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    aggregate_type = table.Column<string>(type: "varchar(60)", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "varchar(80)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                });

            // Partial index — only unpublished events (hot path for outbox worker)
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_outbox_unpublished ON outbox_events(published_at) WHERE published_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "outbox_events");
            migrationBuilder.DropTable(name: "civis_decisions");
            migrationBuilder.DropTable(name: "cluster_event_links");
            migrationBuilder.DropTable(name: "signal_clusters");
        }
    }
}
