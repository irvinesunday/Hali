using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Clusters;

public class ClustersDbContextFactory : IDesignTimeDbContextFactory<ClustersDbContext>
{
    public ClustersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClustersDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme",
            npgsql => npgsql.UseNetTopologySuite());
        return new ClustersDbContext(optionsBuilder.Options);
    }
}
