using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.DataProtection;

/// <summary>
/// Persists the ASP.NET Core Data Protection key ring to PostgreSQL.
/// The key ring protects TOTP secrets for institution + institution-admin
/// users (#197) — losing the ring breaks every second factor, so the
/// ring must survive process restarts and replicate across nodes.
///
/// Column names are snake_cased to match the project's existing schema
/// convention (see AuthDbContext, AdminDbContext etc).
/// </summary>
public sealed class HaliDataProtectionDbContext
    : DbContext, IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public HaliDataProtectionDbContext(DbContextOptions<HaliDataProtectionDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataProtectionKey>(delegate(EntityTypeBuilder<DataProtectionKey> e)
        {
            e.ToTable("data_protection_keys");
            e.HasKey((DataProtectionKey x) => x.Id);
            e.Property((DataProtectionKey x) => x.Id).HasColumnName("id");
            e.Property((DataProtectionKey x) => x.FriendlyName).HasColumnName("friendly_name");
            e.Property((DataProtectionKey x) => x.Xml).HasColumnName("xml");
        });
    }
}
