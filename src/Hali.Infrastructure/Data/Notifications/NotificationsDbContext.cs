using Hali.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Data.Notifications;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Follow> Follows => Set<Follow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(20);
            e.Property(x => x.NotificationType).HasColumnName("notification_type").HasMaxLength(50);
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.SendAfter).HasColumnName("send_after");
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(200);
            e.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("uq_notification_dedupe");
            e.HasIndex(x => x.SendAfter).HasDatabaseName("ix_notifications_queued_send_after")
                .HasFilter("status = 'queued'");
        });

        modelBuilder.Entity<Follow>(e =>
        {
            e.ToTable("follows");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.LocalityId).HasColumnName("locality_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.AccountId, x.LocalityId }).IsUnique().HasDatabaseName("uq_follow");
        });
    }
}
