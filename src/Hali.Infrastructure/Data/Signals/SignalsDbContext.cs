using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContext : DbContext
{
    public SignalsDbContext(DbContextOptions<SignalsDbContext> options) : base(options) { }

    public DbSet<Locality> Localities => Set<Locality>();
    public DbSet<LocationLabel> LocationLabels => Set<LocationLabel>();
    public DbSet<TaxonomyCategory> TaxonomyCategories => Set<TaxonomyCategory>();
    public DbSet<TaxonomyCondition> TaxonomyConditions => Set<TaxonomyCondition>();
    public DbSet<SignalEvent> SignalEvents => Set<SignalEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
        modelBuilder.HasPostgresEnum<LocationPrecisionType>("location_precision_type");

        modelBuilder.Entity<Locality>(e =>
        {
            e.ToTable("localities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(3);
            e.Property(x => x.CountyName).HasColumnName("county_name").HasMaxLength(100);
            e.Property(x => x.CityName).HasColumnName("city_name").HasMaxLength(100);
            e.Property(x => x.WardName).HasColumnName("ward_name").HasMaxLength(100);
            e.Property(x => x.WardCode).HasColumnName("ward_code").HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property<object?>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
        });

        modelBuilder.Entity<LocationLabel>(e =>
        {
            e.ToTable("location_labels");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.AreaName).HasColumnName("area_name").HasMaxLength(200);
            e.Property(x => x.RoadName).HasColumnName("road_name").HasMaxLength(200);
            e.Property(x => x.JunctionDescription).HasColumnName("junction_description").HasMaxLength(300);
            e.Property(x => x.LandmarkName).HasColumnName("landmark_name").HasMaxLength(200);
            e.Property(x => x.FacilityName).HasColumnName("facility_name").HasMaxLength(200);
            e.Property(x => x.LocationLabelText).HasColumnName("location_label").HasMaxLength(400);
            e.Property(x => x.PrecisionType).HasColumnName("precision_type");
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property<object?>("Geom").HasColumnName("geom").HasColumnType("geometry(Point, 4326)");
        });

        modelBuilder.Entity<TaxonomyCategory>(e =>
        {
            e.ToTable("taxonomy_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(60);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.HasIndex(x => new { x.Category, x.SubcategorySlug }).IsUnique().HasDatabaseName("uq_taxonomy_category_slug");
        });

        modelBuilder.Entity<TaxonomyCondition>(e =>
        {
            e.ToTable("taxonomy_conditions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.ConditionSlug).HasColumnName("condition_slug").HasMaxLength(60);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            e.Property(x => x.Ordinal).HasColumnName("ordinal");
            e.Property(x => x.IsPositive).HasColumnName("is_positive");
            e.HasIndex(x => new { x.Category, x.ConditionSlug }).IsUnique().HasDatabaseName("uq_taxonomy_condition_slug");
        });

        modelBuilder.Entity<SignalEvent>(e =>
        {
            e.ToTable("signal_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.LocationLabelId).HasColumnName("location_label_id");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.SubcategorySlug).HasColumnName("subcategory_slug").HasMaxLength(60);
            e.Property(x => x.ConditionSlug).HasColumnName("condition_slug").HasMaxLength(60);
            e.Property(x => x.FreeText).HasColumnName("free_text");
            e.Property(x => x.NeutralSummary).HasColumnName("neutral_summary");
            e.Property(x => x.TemporalType).HasColumnName("temporal_type").HasMaxLength(30);
            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.LocationConfidence).HasColumnName("location_confidence").HasPrecision(4, 3);
            e.Property(x => x.LocationSource).HasColumnName("location_source").HasMaxLength(20);
            e.Property(x => x.ConditionConfidence).HasColumnName("condition_confidence").HasPrecision(4, 3);
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.SourceLanguage).HasColumnName("source_language").HasMaxLength(10);
            e.Property(x => x.SourceChannel).HasColumnName("source_channel").HasMaxLength(20);
            e.Property(x => x.SpatialCellId).HasColumnName("spatial_cell_id").HasMaxLength(20);
            e.Property(x => x.CivisPrecheck).HasColumnName("civis_precheck").HasColumnType("jsonb");
            e.HasIndex(x => new { x.LocalityId, x.Category, x.OccurredAt }).HasDatabaseName("ix_signal_events_locality_category_time");
            e.HasIndex(x => new { x.SpatialCellId, x.OccurredAt }).HasDatabaseName("ix_signal_events_spatial_cell_time");
        });
    }
}
