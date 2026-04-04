using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Participation.Migrations;

[DbContext(typeof(ParticipationDbContext))]
internal class ParticipationDbContextModelSnapshot : ModelSnapshot
{
	protected override void BuildModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.HasPostgresEnum("participation_type", "participation_type", new string[6] { "affected", "observing", "no_longer_affected", "restoration_yes", "restoration_no", "restoration_unsure" });
		modelBuilder.UseIdentityByDefaultColumns();
		modelBuilder.Entity("Hali.Domain.Entities.Participation.Participation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<Guid?>("AccountId").HasColumnType("uuid").HasColumnName("account_id");
			b.Property<Guid>("ClusterId").HasColumnType("uuid").HasColumnName("cluster_id");
			b.Property<string>("ContextText").HasColumnType("text").HasColumnName("context_text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<Guid?>("DeviceId").HasColumnType("uuid").HasColumnName("device_id");
			b.Property<string>("IdempotencyKey").HasMaxLength(100).HasColumnType("character varying(100)")
				.HasColumnName("idempotency_key");
			b.Property<int>("ParticipationType").HasColumnType("integer").HasColumnName("participation_type");
			b.HasKey("Id");
			b.HasIndex("ClusterId", "DeviceId", "ParticipationType", "IdempotencyKey").IsUnique().HasDatabaseName("uq_participation_idempotency");
			b.ToTable("participations", (string?)null);
		});
	}
}
