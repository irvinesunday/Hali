using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Clusters;

public class ClustersDbContextFactory : IDesignTimeDbContextFactory<ClustersDbContext>
{
	public ClustersDbContext CreateDbContext(string[] args)
	{
		var dataSource = HaliNpgsqlDataSourceFactory.Build(
			"Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme",
			useNetTopologySuite: true);
		var dbContextOptionsBuilder = new DbContextOptionsBuilder<ClustersDbContext>();
		dbContextOptionsBuilder.UseNpgsql(dataSource, npgsql => npgsql.UseNetTopologySuite());
		return new ClustersDbContext(dbContextOptionsBuilder.Options);
	}
}
