using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hali.Infrastructure.Data.Clusters.Migrations
{
    /// <inheritdoc />
    public partial class AddClustersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .Annotation("Npgsql:Enum:signal_state.signal_state", "unconfirmed,active,possible_restoration,resolved,expired,suppressed")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "civis_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason_codes = table.Column<string>(type: "jsonb", nullable: true),
                    metrics = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_civis_decisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cluster_event_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cluster_event_links", x => x.id);
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
                name: "signal_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    locality_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<int>(type: "integer", nullable: false),
                    subcategory_slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    dominant_condition_slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    location_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    spatial_cell_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    possible_restoration_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    civis_score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    wrab = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    sds = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    macf = table.Column<int>(type: "integer", nullable: true),
                    raw_confirmation_count = table.Column<int>(type: "integer", nullable: false),
                    temporal_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    affected_count = table.Column<int>(type: "integer", nullable: false),
                    observing_count = table.Column<int>(type: "integer", nullable: false),
                    centroid = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_clusters", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_cluster_event_link",
                table: "cluster_event_links",
                columns: new[] { "cluster_id", "signal_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_events_unpublished",
                table: "outbox_events",
                column: "occurred_at",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_last_seen",
                table: "signal_clusters",
                column: "last_seen_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_spatial_cell_category",
                table: "signal_clusters",
                columns: new[] { "spatial_cell_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_signal_clusters_state_locality_category",
                table: "signal_clusters",
                columns: new[] { "state", "locality_id", "category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "civis_decisions");

            migrationBuilder.DropTable(
                name: "cluster_event_links");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "signal_clusters");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:civic_category.civic_category", "roads,water,electricity,transport,safety,environment,governance,infrastructure")
                .OldAnnotation("Npgsql:Enum:signal_state.signal_state", "unconfirmed,active,possible_restoration,resolved,expired,suppressed")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
