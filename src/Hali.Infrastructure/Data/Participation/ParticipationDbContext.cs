using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;

namespace Hali.Infrastructure.Data.Participation;

public class ParticipationDbContext : DbContext
{
    public ParticipationDbContext(DbContextOptions<ParticipationDbContext> options) : base(options) { }

    public DbSet<ParticipationEntity> Participations => Set<ParticipationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ParticipationType>("participation_type");

        modelBuilder.Entity<ParticipationEntity>(e =>
        {
            e.ToTable("participations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClusterId).HasColumnName("cluster_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.ParticipationType).HasColumnName("participation_type");
            e.Property(x => x.ContextText).HasColumnName("context_text");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);
            e.HasIndex(x => new { x.ClusterId, x.DeviceId, x.ParticipationType, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("uq_participation_idempotency");
        });
    }
}
