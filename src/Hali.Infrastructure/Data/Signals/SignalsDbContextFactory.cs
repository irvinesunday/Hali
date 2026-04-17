using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContextFactory : IDesignTimeDbContextFactory<SignalsDbContext>
{
	public SignalsDbContext CreateDbContext(string[] args)
	{
		var dataSource = HaliNpgsqlDataSourceFactory.Build(
			"Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme",
			useNetTopologySuite: true);
		var dbContextOptionsBuilder = new DbContextOptionsBuilder<SignalsDbContext>();
		dbContextOptionsBuilder.UseNpgsql(dataSource, npgsql => npgsql.UseNetTopologySuite());
		return new SignalsDbContext(dbContextOptionsBuilder.Options);
	}
}
