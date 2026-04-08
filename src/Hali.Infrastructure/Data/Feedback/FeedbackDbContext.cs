using Hali.Domain.Entities.Feedback;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Feedback;

public class FeedbackDbContext : DbContext
{
	public DbSet<AppFeedback> AppFeedback => Set<AppFeedback>();

	public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<AppFeedback>(delegate(EntityTypeBuilder<AppFeedback> e)
		{
			e.ToTable("app_feedback");
			e.HasKey(x => x.Id);

			e.Property(x => x.Id).HasColumnName("id")
				.HasDefaultValueSql("gen_random_uuid()");
			e.Property(x => x.Rating).HasColumnName("rating")
				.HasMaxLength(10).IsRequired();
			e.Property(x => x.Text).HasColumnName("text").HasMaxLength(300);
			e.Property(x => x.Screen).HasColumnName("screen").HasMaxLength(50);
			e.Property(x => x.ClusterId).HasColumnName("cluster_id");
			e.Property(x => x.AccountId).HasColumnName("account_id");
			e.Property(x => x.AppVersion).HasColumnName("app_version").HasMaxLength(20);
			e.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(10);
			e.Property(x => x.SessionId).HasColumnName("session_id");
			e.Property(x => x.SubmittedAt).HasColumnName("submitted_at").IsRequired();

			e.HasIndex(x => x.SubmittedAt).HasDatabaseName("ix_app_feedback_submitted_at");
			e.HasIndex(x => x.Rating).HasDatabaseName("ix_app_feedback_rating");
			e.HasIndex(x => x.Screen).HasDatabaseName("ix_app_feedback_screen");

			// Intentionally no FK constraints — feedback must survive cluster/account deletion.
		});
	}
}
