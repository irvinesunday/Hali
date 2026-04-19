using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Hali.Infrastructure.Data;

namespace Hali.Infrastructure.Data.Marketing;

public class MarketingDbContextFactory : IDesignTimeDbContextFactory<MarketingDbContext>
{
    public MarketingDbContext CreateDbContext(string[] args)
    {
        var dataSource = HaliNpgsqlDataSourceFactory.Build(
            "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme");
        var builder = new DbContextOptionsBuilder<MarketingDbContext>();
        builder.UseNpgsql(dataSource);
        return new MarketingDbContext(builder.Options);
    }
}
