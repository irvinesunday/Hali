using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hali.Infrastructure.Data.Clusters.Migrations;

[DbContext(typeof(ClustersDbContext))]
internal class ClustersDbContextModelSnapshot : ModelSnapshot
{
	protected override void BuildModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
