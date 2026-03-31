using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hali.Infrastructure.Data.Clusters.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS signal_state AS ENUM ('unconfirmed', 'active', 'possible_restoration', 'resolved', 'expired', 'suppressed');");

        migrationBuilder.Sql(@"
CREATE TABLE signal_clusters (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locality_id uuid,
    category civic_category NOT NULL,
    subcategory_slug varchar(60),
    dominant_condition_slug varchar(60),
    state signal_state NOT NULL DEFAULT 'unconfirmed',
    title varchar(300),
    summary text,
    location_label_id uuid,
    centroid geometry(Point, 4326),
    spatial_cell_id varchar(20),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    last_seen_at timestamptz NOT NULL DEFAULT now(),
    civis_score numeric(5,3) DEFAULT 0,
    wrab numeric(5,3) DEFAULT 0,
    sds numeric(5,3) DEFAULT 0,
    macf numeric(5,3) DEFAULT 0,
    raw_confirmation_count integer NOT NULL DEFAULT 0,
    temporal_type varchar(30),
    affected_count integer NOT NULL DEFAULT 0,
    observing_count integer NOT NULL DEFAULT 0
);");

        migrationBuilder.Sql(@"
CREATE TABLE cluster_event_links (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),
    signal_event_id uuid NOT NULL,
    link_reason varchar(50),
    linked_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_cluster_event_link UNIQUE (cluster_id, signal_event_id)
);");

        migrationBuilder.Sql(@"
CREATE TABLE civis_decisions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),
    decision_type varchar(50) NOT NULL,
    reason_codes jsonb,
    metrics jsonb,
    created_at timestamptz NOT NULL DEFAULT now()
);");

        migrationBuilder.Sql(@"
CREATE TABLE outbox_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    aggregate_type varchar(100) NOT NULL,
    aggregate_id uuid NOT NULL,
    event_type varchar(100) NOT NULL,
    payload jsonb,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    published_at timestamptz
);");

        migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_state_locality_category ON signal_clusters(state, locality_id, category);");
        migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_last_seen ON signal_clusters(last_seen_at DESC);");
        migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_spatial_cell_category ON signal_clusters(spatial_cell_id, category);");
        migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_centroid ON signal_clusters USING GIST(centroid);");
        migrationBuilder.Sql("CREATE INDEX ix_outbox_events_unpublished ON outbox_events(occurred_at) WHERE published_at IS NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS civis_decisions;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS cluster_event_links;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS signal_clusters;");
        migrationBuilder.Sql("DROP TYPE IF EXISTS signal_state;");
    }
}
