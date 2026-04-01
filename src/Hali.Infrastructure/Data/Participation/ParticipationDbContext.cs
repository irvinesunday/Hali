using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Participation;

public class ParticipationDbContext : DbContext
{
	public DbSet<Hali.Domain.Entities.Participation.Participation> Participations => Set<Hali.Domain.Entities.Participation.Participation>();

	public ParticipationDbContext(DbContextOptions<ParticipationDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasPostgresEnum<ParticipationType>("participation_type");
		modelBuilder.Entity(delegate(EntityTypeBuilder<Hali.Domain.Entities.Participation.Participation> e)
		{
			e.ToTable("participations");
			e.HasKey((Hali.Domain.Entities.Participation.Participation x) => x.Id);
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.Id).HasColumnName("id");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.ClusterId).HasColumnName("cluster_id");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.AccountId).HasColumnName("account_id");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId).HasColumnName("device_id");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.ParticipationType).HasColumnName("participation_type");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.ContextText).HasColumnName("context_text");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.CreatedAt).HasColumnName("created_at");
			e.Property((Hali.Domain.Entities.Participation.Participation x) => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);
			e.HasIndex((Hali.Domain.Entities.Participation.Participation x) => new { x.ClusterId, x.DeviceId, x.ParticipationType, x.IdempotencyKey }).IsUnique().HasDatabaseName("uq_participation_idempotency");
		});
	}
}
