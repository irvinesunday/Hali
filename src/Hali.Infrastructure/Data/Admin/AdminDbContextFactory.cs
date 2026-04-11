using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Admin;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
	public AdminDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("HALI_DB_CONNECTION")
			?? "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme";
		var builder = new DbContextOptionsBuilder<AdminDbContext>();
		builder.UseNpgsql(connectionString);
		return new AdminDbContext(builder.Options);
	}
}
