using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Hali.Infrastructure.Data.Clusters;

public class ClustersDbContextFactory : IDesignTimeDbContextFactory<ClustersDbContext>
{
	public ClustersDbContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<ClustersDbContext> dbContextOptionsBuilder = new DbContextOptionsBuilder<ClustersDbContext>();
		dbContextOptionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme", delegate(NpgsqlDbContextOptionsBuilder npgsql)
		{
			npgsql.UseNetTopologySuite();
		});
		return new ClustersDbContext(dbContextOptionsBuilder.Options);
	}
}
