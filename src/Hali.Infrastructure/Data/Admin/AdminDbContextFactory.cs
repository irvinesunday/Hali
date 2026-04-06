using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Admin;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
	public AdminDbContext CreateDbContext(string[] args)
	{
		var builder = new DbContextOptionsBuilder<AdminDbContext>();
		builder.UseNpgsql("Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme");
		return new AdminDbContext(builder.Options);
	}
}
