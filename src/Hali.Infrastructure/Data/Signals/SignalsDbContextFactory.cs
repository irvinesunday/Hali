using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hali.Infrastructure.Data.Signals;

public class SignalsDbContextFactory : IDesignTimeDbContextFactory<SignalsDbContext>
{
    public SignalsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SignalsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme",
            npgsql => npgsql.UseNetTopologySuite());
        return new SignalsDbContext(optionsBuilder.Options);
    }
}
