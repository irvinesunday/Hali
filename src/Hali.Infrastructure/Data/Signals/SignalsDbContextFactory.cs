using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContextFactory : IDesignTimeDbContextFactory<SignalsDbContext>
{
	public SignalsDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("HALI_DB_CONNECTION")
			?? "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme";
		DbContextOptionsBuilder<SignalsDbContext> dbContextOptionsBuilder = new DbContextOptionsBuilder<SignalsDbContext>();
		dbContextOptionsBuilder.UseNpgsql(connectionString, delegate(NpgsqlDbContextOptionsBuilder npgsql)
		{
			npgsql.UseNetTopologySuite();
		});
		return new SignalsDbContext(dbContextOptionsBuilder.Options);
	}
}
