using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.DataProtection;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> / <c>database
/// update</c>. Mirrors the pattern used by <see cref="Hali.Infrastructure.Data.Admin.AdminDbContextFactory"/>
/// and the other per-context factories.
/// </summary>
public sealed class HaliDataProtectionDbContextFactory
    : IDesignTimeDbContextFactory<HaliDataProtectionDbContext>
{
    public HaliDataProtectionDbContext CreateDbContext(string[] args)
    {
        var dataSource = HaliNpgsqlDataSourceFactory.Build(
            "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme");
        var builder = new DbContextOptionsBuilder<HaliDataProtectionDbContext>();
        builder.UseNpgsql(dataSource);
        return new HaliDataProtectionDbContext(builder.Options);
    }
}
