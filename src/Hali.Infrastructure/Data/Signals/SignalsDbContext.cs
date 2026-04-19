using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContext : DbContext
{
    public DbSet<Locality> Localities => Set<Locality>();

    public DbSet<LocationLabel> LocationLabels => Set<LocationLabel>();

    public DbSet<TaxonomyCategory> TaxonomyCategories => Set<TaxonomyCategory>();

    public DbSet<TaxonomyCondition> TaxonomyConditions => Set<TaxonomyCondition>();

    public DbSet<SignalEvent> SignalEvents => Set<SignalEvent>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public SignalsDbContext(DbContextOptions<SignalsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
        modelBuilder.HasPostgresEnum<LocationPrecisionType>("location_precision_type");
        modelBuilder.Entity(delegate (EntityTypeBuilder<Locality> e)
        {
            e.ToTable("localities");
            e.HasKey((Locality x) => x.Id);
            e.Property((Locality x) => x.Id).HasColumnName("id");
            e.Property((Locality x) => x.CountryCode).HasColumnName("country_code").HasMaxLength(3);
            e.Property((Locality x) => x.CountyName).HasColumnName("county_name").HasMaxLength(100);
            e.Property((Locality x) => x.CityName).HasColumnName("city_name").HasMaxLength(100);
            e.Property((Locality x) => x.WardName).HasColumnName("ward_name").HasMaxLength(100);
            e.Property((Locality x) => x.WardCode).HasColumnName("ward_code").HasMaxLength(50);
            e.Property((Locality x) => x.CreatedAt).HasColumnName("created_at");
            e.Property<MultiPolygon>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<LocationLabel> e)
        {
            e.ToTable("location_labels");
            e.HasKey((LocationLabel x) => x.Id);
            e.Property((LocationLabel x) => x.Id).HasColumnName("id");
            e.Property((LocationLabel x) => x.LocalityId).HasColumnName("locality_id");
            e.Property((LocationLabel x) => x.AreaName).HasColumnName("area_name").HasMaxLength(200);
            e.Property((LocationLabel x) => x.RoadName).HasColumnName("road_name").HasMaxLength(200);
            e.Property((LocationLabel x) => x.JunctionDescription).HasColumnName("junction_description").HasMaxLength(300);
            e.Property((LocationLabel x) => x.LandmarkName).HasColumnName("landmark_name").HasMaxLength(200);
            e.Property((LocationLabel x) => x.FacilityName).HasColumnName("facility_name").HasMaxLength(200);
            e.Property((LocationLabel x) => x.LocationLabelText).HasColumnName("location_label").HasMaxLength(400);
            e.Property((LocationLabel x) => x.PrecisionType).HasColumnName("precision_type");
            e.Property((LocationLabel x) => x.Latitude).HasColumnName("latitude");
            e.Property((LocationLabel x) => x.Longitude).HasColumnName("longitude");
            e.Property((LocationLabel x) => x.CreatedAt).HasColumnName("created_at");
            e.Property<Point>("Geom").HasColumnName("geom").HasColumnType("geometry(Point, 4326)");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<TaxonomyCategory> e)
        {
            e.ToTable("taxonomy_categories");
            e.HasKey((TaxonomyCategory x) => x.Id);
            e.Property((TaxonomyCategory x) => x.Id).HasColumnName("id");
            e.Property((TaxonomyCategory x) => x.Category).HasColumnName("category");
            e.Property((TaxonomyCategory x) => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(60);
            e.Property((TaxonomyCategory x) => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            e.Property((TaxonomyCategory x) => x.Description).HasColumnName("description");
            e.Property((TaxonomyCategory x) => x.IsActive).HasColumnName("is_active");
            e.HasIndex((TaxonomyCategory x) => new { x.Category, x.SubcategorySlug }).IsUnique().HasDatabaseName("uq_taxonomy_category_slug");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<TaxonomyCondition> e)
        {
            e.ToTable("taxonomy_conditions");
            e.HasKey((TaxonomyCondition x) => x.Id);
            e.Property((TaxonomyCondition x) => x.Id).HasColumnName("id");
            e.Property((TaxonomyCondition x) => x.Category).HasColumnName("category");
            e.Property((TaxonomyCondition x) => x.ConditionSlug).HasColumnName("condition_slug").HasMaxLength(60);
            e.Property((TaxonomyCondition x) => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            e.Property((TaxonomyCondition x) => x.Ordinal).HasColumnName("ordinal");
            e.Property((TaxonomyCondition x) => x.IsPositive).HasColumnName("is_positive");
            e.HasIndex((TaxonomyCondition x) => new { x.Category, x.ConditionSlug }).IsUnique().HasDatabaseName("uq_taxonomy_condition_slug");
        });
        modelBuilder.Entity(delegate (EntityTypeBuilder<SignalEvent> e)
        {
            e.ToTable("signal_events");
            e.HasKey((SignalEvent x) => x.Id);
            e.Property((SignalEvent x) => x.Id).HasColumnName("id");
            e.Property((SignalEvent x) => x.AccountId).HasColumnName("account_id");
            e.Property((SignalEvent x) => x.DeviceId).HasColumnName("device_id");
            e.Property((SignalEvent x) => x.LocalityId).HasColumnName("locality_id");
            e.Property((SignalEvent x) => x.LocationLabelId).HasColumnName("location_label_id");
            e.Property((SignalEvent x) => x.Category).HasColumnName("category");
            e.Property((SignalEvent x) => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(60);
            e.Property((SignalEvent x) => x.ConditionSlug).HasColumnName("condition_slug").HasMaxLength(60);
            e.Property((SignalEvent x) => x.FreeText).HasColumnName("free_text");
            e.Property((SignalEvent x) => x.NeutralSummary).HasColumnName("neutral_summary");
            e.Property((SignalEvent x) => x.TemporalType).HasColumnName("temporal_type").HasMaxLength(30);
            e.Property((SignalEvent x) => x.Latitude).HasColumnName("latitude");
            e.Property((SignalEvent x) => x.Longitude).HasColumnName("longitude");
            e.Property((SignalEvent x) => x.LocationConfidence).HasColumnName("location_confidence").HasPrecision(4, 3);
            e.Property((SignalEvent x) => x.LocationSource).HasColumnName("location_source").HasMaxLength(20);
            e.Property((SignalEvent x) => x.ConditionConfidence).HasColumnName("condition_confidence").HasPrecision(4, 3);
            e.Property((SignalEvent x) => x.OccurredAt).HasColumnName("occurred_at");
            e.Property((SignalEvent x) => x.CreatedAt).HasColumnName("created_at");
            e.Property((SignalEvent x) => x.SourceLanguage).HasColumnName("source_language").HasMaxLength(10);
            e.Property((SignalEvent x) => x.SourceChannel).HasColumnName("source_channel").HasMaxLength(20);
            e.Property((SignalEvent x) => x.SpatialCellId).HasColumnName("spatial_cell_id").HasMaxLength(20);
            e.Property((SignalEvent x) => x.CivisPrecheck).HasColumnName("civis_precheck").HasColumnType("jsonb");
            e.Ignore((SignalEvent x) => x.LocationLabelText);
            e.HasIndex((SignalEvent x) => new { x.LocalityId, x.Category, x.OccurredAt }).HasDatabaseName("ix_signal_events_locality_category_time");
            e.HasIndex((SignalEvent x) => new { x.SpatialCellId, x.OccurredAt }).HasDatabaseName("ix_signal_events_spatial_cell_time");
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
        });
    }
}
