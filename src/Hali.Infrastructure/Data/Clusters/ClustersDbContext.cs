using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Data.Clusters;

public class ClustersDbContext : DbContext
{
    public ClustersDbContext(DbContextOptions<ClustersDbContext> options) : base(options) { }

    public DbSet<SignalCluster> SignalClusters => Set<SignalCluster>();
    public DbSet<ClusterEventLink> ClusterEventLinks => Set<ClusterEventLink>();
    public DbSet<CivisDecision> CivisDecisions => Set<CivisDecision>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
        modelBuilder.HasPostgresEnum<SignalState>("signal_state");

        modelBuilder.Entity<SignalCluster>(e =>
        {
            e.ToTable("signal_clusters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(60);
            e.Property(x => x.DominantConditionSlug).HasColumnName("dominant_condition_slug").HasMaxLength(60);
            e.Property(x => x.State).HasColumnName("state");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            e.Property(x => x.Summary).HasColumnName("summary");
            e.Property(x => x.LocationLabelId).HasColumnName("location_label_id");
            e.Property(x => x.SpatialCellId).HasColumnName("spatial_cell_id").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            e.Property(x => x.CivisScore).HasColumnName("civis_score").HasPrecision(5, 3);
            e.Property(x => x.Wrab).HasColumnName("wrab").HasPrecision(5, 3);
            e.Property(x => x.Sds).HasColumnName("sds").HasPrecision(5, 3);
            e.Property(x => x.Macf).HasColumnName("macf").HasPrecision(5, 3);
            e.Property(x => x.RawConfirmationCount).HasColumnName("raw_confirmation_count");
            e.Property(x => x.TemporalType).HasColumnName("temporal_type").HasMaxLength(30);
            e.Property(x => x.AffectedCount).HasColumnName("affected_count");
            e.Property(x => x.ObservingCount).HasColumnName("observing_count");
            e.Property<object?>("Centroid").HasColumnName("centroid").HasColumnType("geometry(Point, 4326)");
            e.HasIndex(x => new { x.State, x.LocalityId, x.Category }).HasDatabaseName("ix_signal_clusters_state_locality_category");
            e.HasIndex(x => x.LastSeenAt).IsDescending().HasDatabaseName("ix_signal_clusters_last_seen");
            e.HasIndex(x => new { x.SpatialCellId, x.Category }).HasDatabaseName("ix_signal_clusters_spatial_cell_category");
        });

        modelBuilder.Entity<ClusterEventLink>(e =>
        {
            e.ToTable("cluster_event_links");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClusterId).HasColumnName("cluster_id");
            e.Property(x => x.SignalEventId).HasColumnName("signal_event_id");
            e.Property(x => x.LinkReason).HasColumnName("link_reason").HasMaxLength(50);
            e.Property(x => x.LinkedAt).HasColumnName("linked_at");
            e.HasIndex(x => new { x.ClusterId, x.SignalEventId }).IsUnique().HasDatabaseName("uq_cluster_event_link");
        });

        modelBuilder.Entity<CivisDecision>(e =>
        {
            e.ToTable("civis_decisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClusterId).HasColumnName("cluster_id");
            e.Property(x => x.DecisionType).HasColumnName("decision_type").HasMaxLength(50);
            e.Property(x => x.ReasonCodes).HasColumnName("reason_codes").HasColumnType("jsonb");
            e.Property(x => x.Metrics).HasColumnName("metrics").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<OutboxEvent>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
            e.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_outbox_events_unpublished")
                .HasFilter("published_at IS NULL");
        });
    }
}
