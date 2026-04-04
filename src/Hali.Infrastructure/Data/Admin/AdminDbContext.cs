using Hali.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Admin;

public class AdminDbContext : DbContext
{
	public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

	public AdminDbContext(DbContextOptions<AdminDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity(delegate(EntityTypeBuilder<AdminAuditLog> e)
		{
			e.ToTable("admin_audit_logs");
			e.HasKey((AdminAuditLog x) => x.Id);
			e.Property((AdminAuditLog x) => x.Id).HasColumnName("id");
			e.Property((AdminAuditLog x) => x.ActorAccountId).HasColumnName("actor_account_id");
			e.Property((AdminAuditLog x) => x.Action).HasColumnName("action").HasMaxLength(100);
			e.Property((AdminAuditLog x) => x.TargetType).HasColumnName("target_type").HasMaxLength(50);
			e.Property((AdminAuditLog x) => x.TargetId).HasColumnName("target_id");
			e.Property((AdminAuditLog x) => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
			e.Property((AdminAuditLog x) => x.CreatedAt).HasColumnName("created_at");
		});
	}
}
