using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace Hali.Infrastructure.Data.Clusters;

public class ClustersDbContext : DbContext
{
    public DbSet<SignalCluster> SignalClusters => Set<SignalCluster>();

    public DbSet<ClusterEventLink> ClusterEventLinks => Set<ClusterEventLink>();

    public DbSet<CivisDecision> CivisDecisions => Set<CivisDecision>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public ClustersDbContext(DbContextOptions<ClustersDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
        modelBuilder.HasPostgresEnum<SignalState>("signal_state");
        modelBuilder.Entity(delegate (EntityTypeBuilder<SignalCluster> e)
        {
            e.ToTable("signal_clusters");
            e.HasKey((SignalCluster x) => x.Id);
            e.Property((SignalCluster x) => x.Id).HasColumnName("id");
            e.Property((SignalCluster x) => x.LocalityId).HasColumnName("locality_id");
            e.Property((SignalCluster x) => x.Category).HasColumnName("category");
            e.Property((SignalCluster x) => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(80);
            e.Property((SignalCluster x) => x.DominantConditionSlug).HasColumnName("dominant_condition_slug").HasMaxLength(80);
            e.Property((SignalCluster x) => x.State).HasColumnName("state");
            e.Property((SignalCluster x) => x.Title).HasColumnName("title").HasMaxLength(240);
            e.Property((SignalCluster x) => x.Summary).HasColumnName("summary");
            e.Property((SignalCluster x) => x.LocationLabelId).HasColumnName("location_label_id");
            e.Property((SignalCluster x) => x.LocationLabelText).HasColumnName("location_label_text").HasMaxLength(400);
            e.Property((SignalCluster x) => x.SpatialCellId).HasColumnName("spatial_cell_id").HasMaxLength(80);
            e.Ignore((SignalCluster x) => x.CreatedAt);
            e.Ignore((SignalCluster x) => x.UpdatedAt);
            e.Property((SignalCluster x) => x.FirstSeenAt).HasColumnName("first_seen_at");
            e.Property((SignalCluster x) => x.LastSeenAt).HasColumnName("last_seen_at");
            e.Property((SignalCluster x) => x.ActivatedAt).HasColumnName("activated_at");
            e.Property((SignalCluster x) => x.ResolvedAt).HasColumnName("resolved_at");
            e.Property((SignalCluster x) => x.PossibleRestorationAt).HasColumnName("possible_restoration_at");
            e.Property((SignalCluster x) => x.CivisScore).HasColumnName("civis_score").HasPrecision(8, 4);
            e.Property((SignalCluster x) => x.Wrab).HasColumnName("wrab").HasPrecision(10, 4);
            e.Property((SignalCluster x) => x.Sds).HasColumnName("sds").HasPrecision(10, 4);
            e.Property((SignalCluster x) => x.Macf).HasColumnName("macf");
            e.Property((SignalCluster x) => x.RawConfirmationCount).HasColumnName("raw_confirmation_count");
            e.Property((SignalCluster x) => x.TemporalType).HasColumnName("temporal_type").HasMaxLength(40);
            e.Property((SignalCluster x) => x.AffectedCount).HasColumnName("affected_count");
            e.Property((SignalCluster x) => x.ObservingCount).HasColumnName("observing_count");
            e.Property<Point>("Centroid").HasColumnName("centroid").HasColumnType("geometry(Point, 4326)");
            e.HasIndex((SignalCluster x) => new { x.State, x.LocalityId, x.Category }).HasDatabaseName("ix_signal_clusters_state_locality_category");
            e.HasIndex((SignalCluster x) => x.LastSeenAt).IsDescending().HasDatabaseName("ix_signal_clusters_last_seen");
            e.HasIndex((SignalCluster x) => new { x.SpatialCellId, x.Category }).HasDatabaseName("ix_signal_clusters_spatial_cell_category");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<ClusterEventLink> e)
        {
            e.ToTable("cluster_event_links");
            e.HasKey((ClusterEventLink x) => x.Id);
            e.Property((ClusterEventLink x) => x.Id).HasColumnName("id");
            e.Property((ClusterEventLink x) => x.ClusterId).HasColumnName("cluster_id");
            e.Property((ClusterEventLink x) => x.SignalEventId).HasColumnName("signal_event_id");
            e.Ignore((ClusterEventLink x) => x.DeviceId);
            e.Property((ClusterEventLink x) => x.LinkReason).HasColumnName("link_reason").HasMaxLength(50);
            e.Property((ClusterEventLink x) => x.LinkedAt).HasColumnName("linked_at");
            e.HasIndex((ClusterEventLink x) => new { x.ClusterId, x.SignalEventId }).IsUnique().HasDatabaseName("uq_cluster_event_link");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<CivisDecision> e)
        {
            e.ToTable("civis_decisions");
            e.HasKey((CivisDecision x) => x.Id);
            e.Property((CivisDecision x) => x.Id).HasColumnName("id");
            e.Property((CivisDecision x) => x.ClusterId).HasColumnName("cluster_id");
            e.Property((CivisDecision x) => x.DecisionType).HasColumnName("decision_type").HasMaxLength(50);
            e.Property((CivisDecision x) => x.ReasonCodes).HasColumnName("reason_codes").HasColumnType("jsonb");
            e.Property((CivisDecision x) => x.Metrics).HasColumnName("metrics").HasColumnType("jsonb");
            e.Property((CivisDecision x) => x.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<OutboxEvent> e)
        {
            e.ToTable("outbox_events");
            e.HasKey((OutboxEvent x) => x.Id);
            e.Property((OutboxEvent x) => x.Id).HasColumnName("id");
            e.Property((OutboxEvent x) => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
            e.Property((OutboxEvent x) => x.AggregateId).HasColumnName("aggregate_id");
            e.Property((OutboxEvent x) => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property((OutboxEvent x) => x.SchemaVersion).HasColumnName("schema_version").HasMaxLength(20).IsRequired();
            e.Property((OutboxEvent x) => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property((OutboxEvent x) => x.OccurredAt).HasColumnName("occurred_at");
            e.Property((OutboxEvent x) => x.PublishedAt).HasColumnName("published_at");
            e.HasIndex((OutboxEvent x) => x.OccurredAt).HasDatabaseName("ix_outbox_events_unpublished").HasFilter("published_at IS NULL");
        });
    }
}
