using Hali.Domain.Entities.Advisories;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Advisories;

public class AdvisoriesDbContext : DbContext
{
	public DbSet<Institution> Institutions => Set<Institution>();

	public DbSet<InstitutionJurisdiction> InstitutionJurisdictions => Set<InstitutionJurisdiction>();

	public DbSet<OfficialPost> OfficialPosts => Set<OfficialPost>();

	public DbSet<OfficialPostScope> OfficialPostScopes => Set<OfficialPostScope>();

	public AdvisoriesDbContext(DbContextOptions<AdvisoriesDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasPostgresEnum<CivicCategory>("civic_category");
		modelBuilder.HasPostgresEnum<OfficialPostType>("official_post_type");
		modelBuilder.Entity(delegate(EntityTypeBuilder<Institution> e)
		{
			e.ToTable("institutions");
			e.HasKey((Institution x) => x.Id);
			e.Property((Institution x) => x.Id).HasColumnName("id");
			e.Property((Institution x) => x.Name).HasColumnName("name").HasMaxLength(200);
			e.Property((Institution x) => x.Type).HasColumnName("type").HasMaxLength(50);
			e.Property((Institution x) => x.JurisdictionLabel).HasColumnName("jurisdiction_label").HasMaxLength(200);
			e.Property((Institution x) => x.IsVerified).HasColumnName("is_verified");
			e.Property((Institution x) => x.CreatedAt).HasColumnName("created_at");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<InstitutionJurisdiction> e)
		{
			e.ToTable("institution_jurisdictions");
			e.HasKey((InstitutionJurisdiction x) => x.Id);
			e.Property((InstitutionJurisdiction x) => x.Id).HasColumnName("id");
			e.Property((InstitutionJurisdiction x) => x.InstitutionId).HasColumnName("institution_id");
			e.Property((InstitutionJurisdiction x) => x.LocalityId).HasColumnName("locality_id");
			e.Property((InstitutionJurisdiction x) => x.CorridorName).HasColumnName("corridor_name").HasMaxLength(200);
			e.Property((InstitutionJurisdiction x) => x.CreatedAt).HasColumnName("created_at");
			e.Property<object>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<OfficialPost> e)
		{
			e.ToTable("official_posts");
			e.HasKey((OfficialPost x) => x.Id);
			e.Property((OfficialPost x) => x.Id).HasColumnName("id");
			e.Property((OfficialPost x) => x.InstitutionId).HasColumnName("institution_id");
			e.Property((OfficialPost x) => x.AuthorAccountId).HasColumnName("author_account_id");
			e.Property((OfficialPost x) => x.Type).HasColumnName("type");
			e.Property((OfficialPost x) => x.Category).HasColumnName("category");
			e.Property((OfficialPost x) => x.Title).HasColumnName("title").HasMaxLength(300);
			e.Property((OfficialPost x) => x.Body).HasColumnName("body");
			e.Property((OfficialPost x) => x.StartsAt).HasColumnName("starts_at");
			e.Property((OfficialPost x) => x.EndsAt).HasColumnName("ends_at");
			e.Property((OfficialPost x) => x.Status).HasColumnName("status").HasMaxLength(20);
			e.Property((OfficialPost x) => x.RelatedClusterId).HasColumnName("related_cluster_id");
			e.Property((OfficialPost x) => x.IsRestorationClaim).HasColumnName("is_restoration_claim");
			e.Property((OfficialPost x) => x.CreatedAt).HasColumnName("created_at");
			e.Property((OfficialPost x) => x.UpdatedAt).HasColumnName("updated_at");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<OfficialPostScope> e)
		{
			e.ToTable("official_post_scopes");
			e.HasKey((OfficialPostScope x) => x.Id);
			e.Property((OfficialPostScope x) => x.Id).HasColumnName("id");
			e.Property((OfficialPostScope x) => x.OfficialPostId).HasColumnName("official_post_id");
			e.Property((OfficialPostScope x) => x.LocalityId).HasColumnName("locality_id");
			e.Property((OfficialPostScope x) => x.CorridorName).HasColumnName("corridor_name").HasMaxLength(200);
			e.Property<object>("Geom").HasColumnName("geom").HasColumnType("geometry(MultiPolygon, 4326)");
		});
	}
}
