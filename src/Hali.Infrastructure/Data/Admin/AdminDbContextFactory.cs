using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Hali.Infrastructure.Data;

namespace Hali.Infrastructure.Data.Admin;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
	public AdminDbContext CreateDbContext(string[] args)
	{
		var dataSource = HaliNpgsqlDataSourceFactory.Build(
			"Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme");
		var builder = new DbContextOptionsBuilder<AdminDbContext>();
		builder.UseNpgsql(dataSource);
		return new AdminDbContext(builder.Options);
	}
}
