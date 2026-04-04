using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hali.Infrastructure.Data.Clusters.Migrations;

[DbContext(typeof(ClustersDbContext))]
[Migration("20260401000000_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE TYPE IF NOT EXISTS signal_state AS ENUM ('unconfirmed', 'active', 'possible_restoration', 'resolved', 'expired', 'suppressed');");
		migrationBuilder.Sql("\nCREATE TABLE signal_clusters (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    locality_id uuid,\n    category civic_category NOT NULL,\n    subcategory_slug varchar(60),\n    dominant_condition_slug varchar(60),\n    state signal_state NOT NULL DEFAULT 'unconfirmed',\n    title varchar(300),\n    summary text,\n    location_label_id uuid,\n    centroid geometry(Point, 4326),\n    spatial_cell_id varchar(20),\n    created_at timestamptz NOT NULL DEFAULT now(),\n    updated_at timestamptz NOT NULL DEFAULT now(),\n    last_seen_at timestamptz NOT NULL DEFAULT now(),\n    civis_score numeric(5,3) DEFAULT 0,\n    wrab numeric(5,3) DEFAULT 0,\n    sds numeric(5,3) DEFAULT 0,\n    macf numeric(5,3) DEFAULT 0,\n    raw_confirmation_count integer NOT NULL DEFAULT 0,\n    temporal_type varchar(30),\n    affected_count integer NOT NULL DEFAULT 0,\n    observing_count integer NOT NULL DEFAULT 0\n);");
		migrationBuilder.Sql("\nCREATE TABLE cluster_event_links (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),\n    signal_event_id uuid NOT NULL,\n    link_reason varchar(50),\n    linked_at timestamptz NOT NULL DEFAULT now(),\n    CONSTRAINT uq_cluster_event_link UNIQUE (cluster_id, signal_event_id)\n);");
		migrationBuilder.Sql("\nCREATE TABLE civis_decisions (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    cluster_id uuid NOT NULL REFERENCES signal_clusters(id),\n    decision_type varchar(50) NOT NULL,\n    reason_codes jsonb,\n    metrics jsonb,\n    created_at timestamptz NOT NULL DEFAULT now()\n);");
		migrationBuilder.Sql("\nCREATE TABLE outbox_events (\n    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),\n    aggregate_type varchar(100) NOT NULL,\n    aggregate_id uuid NOT NULL,\n    event_type varchar(100) NOT NULL,\n    payload jsonb,\n    occurred_at timestamptz NOT NULL DEFAULT now(),\n    published_at timestamptz\n);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_state_locality_category ON signal_clusters(state, locality_id, category);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_last_seen ON signal_clusters(last_seen_at DESC);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_spatial_cell_category ON signal_clusters(spatial_cell_id, category);");
		migrationBuilder.Sql("CREATE INDEX ix_signal_clusters_centroid ON signal_clusters USING GIST(centroid);");
		migrationBuilder.Sql("CREATE INDEX ix_outbox_events_unpublished ON outbox_events(occurred_at) WHERE published_at IS NULL;");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP TABLE IF EXISTS outbox_events;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS civis_decisions;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS cluster_event_links;");
		migrationBuilder.Sql("DROP TABLE IF EXISTS signal_clusters;");
		migrationBuilder.Sql("DROP TYPE IF EXISTS signal_state;");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
