using Hali.Domain.Entities.Advisories;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Data.Advisories;

public class AdvisoriesDbContext : DbContext
{
    public AdvisoriesDbContext(DbContextOptions<AdvisoriesDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<InstitutionJurisdiction> InstitutionJurisdictions => Set<InstitutionJurisdiction>();
    public DbSet<OfficialPost> OfficialPosts => Set<OfficialPost>();
    public DbSet<OfficialPostScope> OfficialPostScopes => Set<OfficialPostScope>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
        modelBuilder.HasPostgresEnum<OfficialPostType>("official_post_type");

        modelBuilder.Entity<Institution>(e =>
        {
            e.ToTable("institutions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(50);
            e.Property(x => x.JurisdictionLabel).HasColumnName("jurisdiction_label").HasMaxLength(200);
            e.Property(x => x.IsVerified).HasColumnName("is_verified");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<InstitutionJurisdiction>(e =>
        {
            e.ToTable("institution_jurisdictions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InstitutionId).HasColumnName("institution_id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.CorridorName).HasColumnName("corridor_name").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property<object?>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
        });

        modelBuilder.Entity<OfficialPost>(e =>
        {
            e.ToTable("official_posts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InstitutionId).HasColumnName("institution_id");
            e.Property(x => x.AuthorAccountId).HasColumnName("author_account_id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            e.Property(x => x.Body).HasColumnName("body");
            e.Property(x => x.StartsAt).HasColumnName("starts_at");
            e.Property(x => x.EndsAt).HasColumnName("ends_at");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.RelatedClusterId).HasColumnName("related_cluster_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OfficialPostScope>(e =>
        {
            e.ToTable("official_post_scopes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OfficialPostId).HasColumnName("official_post_id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.CorridorName).HasColumnName("corridor_name").HasMaxLength(200);
            e.Property<object?>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
        });
    }
}
