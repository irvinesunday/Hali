using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hali.Infrastructure.Data.Admin.Migrations;

[DbContext(typeof(AdminDbContext))]
internal class AdminDbContextModelSnapshot : ModelSnapshot
{
	protected override void BuildModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
	}
}
