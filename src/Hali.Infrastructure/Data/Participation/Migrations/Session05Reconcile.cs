using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Hali.Infrastructure.Data.Participation.Migrations;

[DbContext(typeof(ParticipationDbContext))]
[Migration("20260401150800_Session05Reconcile")]
public class Session05Reconcile : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterDatabase().Annotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");
		migrationBuilder.CreateTable("participations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> cluster_id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> account_id = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> device_id = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> participation_type = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> context_text = table.Column<string>("text", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> created_at = table.Column<DateTime>("timestamp with time zone");
			int? maxLength = 100;
			return new
			{
				id = id,
				cluster_id = cluster_id,
				account_id = account_id,
				device_id = device_id,
				participation_type = participation_type,
				context_text = context_text,
				created_at = created_at,
				idempotency_key = table.Column<string>("character varying(100)", null, maxLength, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_participations", x => x.id);
		});
		migrationBuilder.CreateIndex("uq_participation_idempotency", "participations", new string[4] { "cluster_id", "device_id", "participation_type", "idempotency_key" }, null, unique: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("participations");
		migrationBuilder.AlterDatabase().OldAnnotation("Npgsql:Enum:participation_type.participation_type", "affected,observing,no_longer_affected,restoration_yes,restoration_no,restoration_unsure");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
