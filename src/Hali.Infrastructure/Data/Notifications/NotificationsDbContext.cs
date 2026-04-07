using Hali.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Notifications;

public class NotificationsDbContext : DbContext
{
	public DbSet<Notification> Notifications => Set<Notification>();

	public DbSet<Follow> Follows => Set<Follow>();

	public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity(delegate(EntityTypeBuilder<Notification> e)
		{
			e.ToTable("notifications");
			e.HasKey((Notification x) => x.Id);
			e.Property((Notification x) => x.Id).HasColumnName("id");
			e.Property((Notification x) => x.AccountId).HasColumnName("account_id");
			e.Property((Notification x) => x.Channel).HasColumnName("channel").HasMaxLength(20);
			e.Property((Notification x) => x.NotificationType).HasColumnName("notification_type").HasMaxLength(50);
			e.Property((Notification x) => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
			e.Property((Notification x) => x.SendAfter).HasColumnName("send_after");
			e.Property((Notification x) => x.SentAt).HasColumnName("sent_at");
			e.Property((Notification x) => x.Status).HasColumnName("status").HasMaxLength(20);
			e.Property((Notification x) => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(200);
			e.HasIndex((Notification x) => x.DedupeKey).IsUnique().HasDatabaseName("uq_notification_dedupe");
			e.HasIndex((Notification x) => x.SendAfter).HasDatabaseName("ix_notifications_queued_send_after").HasFilter("status = 'queued'");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<Follow> e)
		{
			e.ToTable("follows");
			e.HasKey((Follow x) => x.Id);
			e.Property((Follow x) => x.Id).HasColumnName("id");
			e.Property((Follow x) => x.AccountId).HasColumnName("account_id");
			e.Property((Follow x) => x.LocalityId).HasColumnName("locality_id");
			e.Property((Follow x) => x.DisplayLabel).HasColumnName("display_label").HasMaxLength(160);
			e.Property((Follow x) => x.CreatedAt).HasColumnName("created_at");
			e.HasIndex((Follow x) => new { x.AccountId, x.LocalityId }).IsUnique().HasDatabaseName("uq_follow");
		});
	}
}
