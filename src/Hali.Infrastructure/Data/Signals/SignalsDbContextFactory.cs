using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContextFactory : IDesignTimeDbContextFactory<SignalsDbContext>
{
	public SignalsDbContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<SignalsDbContext> dbContextOptionsBuilder = new DbContextOptionsBuilder<SignalsDbContext>();
		dbContextOptionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme", delegate(NpgsqlDbContextOptionsBuilder npgsql)
		{
			npgsql.UseNetTopologySuite();
		});
		return new SignalsDbContext(dbContextOptionsBuilder.Options);
	}
}
