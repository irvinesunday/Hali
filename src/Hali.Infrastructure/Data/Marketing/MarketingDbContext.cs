using Hali.Domain.Entities.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Marketing;

public class MarketingDbContext : DbContext
{
    public DbSet<EarlyAccessSignup> EarlyAccessSignups => Set<EarlyAccessSignup>();

    public DbSet<InstitutionInquiry> InstitutionInquiries => Set<InstitutionInquiry>();

    public MarketingDbContext(DbContextOptions<MarketingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity(delegate (EntityTypeBuilder<EarlyAccessSignup> e)
        {
            e.ToTable("early_access_signups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
            e.Property(x => x.SubmittedAt).HasColumnName("submitted_at").IsRequired();
            e.HasIndex(x => x.Email).HasDatabaseName("ix_early_access_signups_email");
            e.HasIndex(x => x.SubmittedAt).HasDatabaseName("ix_early_access_signups_submitted_at");
        });

        modelBuilder.Entity(delegate (EntityTypeBuilder<InstitutionInquiry> e)
        {
            e.ToTable("institution_inquiries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(x => x.Organisation).HasColumnName("organisation").HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
            e.Property(x => x.Area).HasColumnName("area").HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            e.Property(x => x.Message).HasColumnName("message").HasMaxLength(500);
            e.Property(x => x.SubmittedAt).HasColumnName("submitted_at").IsRequired();
            e.HasIndex(x => x.Email).HasDatabaseName("ix_institution_inquiries_email");
            e.HasIndex(x => x.SubmittedAt).HasDatabaseName("ix_institution_inquiries_submitted_at");
        });
    }
}
