using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260401150751_Session05Reconcile")]
public class Session05Reconcile : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AlterDatabase().Annotation("Npgsql:Enum:account_type.account_type", "citizen,institution_user,admin").Annotation("Npgsql:Enum:auth_method.auth_method", "phone_otp,email_otp,magic_link,google,apple");
		migrationBuilder.CreateTable("accounts", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> account_type = table.Column<int>("integer");
			int? maxLength = 120;
			OperationBuilder<AddColumnOperation> display_name = table.Column<string>("character varying(120)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 254;
			OperationBuilder<AddColumnOperation> email = table.Column<string>("character varying(254)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 20;
			OperationBuilder<AddColumnOperation> phone_e = table.Column<string>("character varying(20)", null, maxLength, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> is_phone_verified = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> is_email_verified = table.Column<bool>("boolean");
			maxLength = 20;
			return new
			{
				id = id,
				account_type = account_type,
				display_name = display_name,
				email = email,
				phone_e164 = phone_e,
				is_phone_verified = is_phone_verified,
				is_email_verified = is_email_verified,
				status = table.Column<string>("character varying(20)", null, maxLength),
				created_at = table.Column<DateTime>("timestamp with time zone"),
				updated_at = table.Column<DateTime>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_accounts", x => x.id);
		});
		migrationBuilder.CreateTable("devices", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> account_id = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			int? maxLength = 64;
			OperationBuilder<AddColumnOperation> device_fingerprint_hash = table.Column<string>("character varying(64)", null, maxLength);
			maxLength = 20;
			OperationBuilder<AddColumnOperation> integrity_level = table.Column<string>("character varying(20)", null, maxLength);
			maxLength = 20;
			OperationBuilder<AddColumnOperation> platform = table.Column<string>("character varying(20)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 20;
			OperationBuilder<AddColumnOperation> app_version = table.Column<string>("character varying(20)", null, maxLength, rowVersion: false, null, nullable: true);
			maxLength = 200;
			return new
			{
				id = id,
				account_id = account_id,
				device_fingerprint_hash = device_fingerprint_hash,
				integrity_level = integrity_level,
				platform = platform,
				app_version = app_version,
				expo_push_token = table.Column<string>("character varying(200)", null, maxLength, rowVersion: false, null, nullable: true),
				created_at = table.Column<DateTime>("timestamp with time zone"),
				last_seen_at = table.Column<DateTime>("timestamp with time zone"),
				is_blocked = table.Column<bool>("boolean")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_devices", x => x.id);
		});
		migrationBuilder.CreateTable("otp_challenges", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> account_id = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> auth_method = table.Column<int>("integer");
			int? maxLength = 254;
			OperationBuilder<AddColumnOperation> destination = table.Column<string>("character varying(254)", null, maxLength);
			maxLength = 128;
			return new
			{
				id = id,
				account_id = account_id,
				auth_method = auth_method,
				destination = destination,
				otp_hash = table.Column<string>("character varying(128)", null, maxLength),
				expires_at = table.Column<DateTime>("timestamp with time zone"),
				consumed_at = table.Column<DateTime>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				created_at = table.Column<DateTime>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_otp_challenges", x => x.id);
		});
		migrationBuilder.CreateTable("refresh_tokens", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength = 128;
			return new
			{
				id = id,
				token_hash = table.Column<string>("character varying(128)", null, maxLength),
				account_id = table.Column<Guid>("uuid"),
				device_id = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				created_at = table.Column<DateTime>("timestamp with time zone"),
				expires_at = table.Column<DateTime>("timestamp with time zone"),
				revoked_at = table.Column<DateTime>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_refresh_tokens", x => x.id);
		});
		migrationBuilder.CreateIndex("uq_accounts_email", "accounts", "email", null, unique: true);
		migrationBuilder.CreateIndex("uq_accounts_phone", "accounts", "phone_e164", null, unique: true);
		migrationBuilder.CreateIndex("ix_refresh_tokens_account", "refresh_tokens", "account_id");
		migrationBuilder.CreateIndex("ix_refresh_tokens_device", "refresh_tokens", "device_id");
		migrationBuilder.CreateIndex("ix_refresh_tokens_expires", "refresh_tokens", "expires_at");
		migrationBuilder.CreateIndex("uq_refresh_tokens_hash", "refresh_tokens", "token_hash", null, unique: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("accounts");
		migrationBuilder.DropTable("devices");
		migrationBuilder.DropTable("otp_challenges");
		migrationBuilder.DropTable("refresh_tokens");
		migrationBuilder.AlterDatabase().OldAnnotation("Npgsql:Enum:account_type.account_type", "citizen,institution_user,admin").OldAnnotation("Npgsql:Enum:auth_method.auth_method", "phone_otp,email_otp,magic_link,google,apple");
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.5").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.HasPostgresEnum("account_type", "account_type", new string[3] { "citizen", "institution_user", "admin" });
		modelBuilder.HasPostgresEnum("auth_method", "auth_method", new string[5] { "phone_otp", "email_otp", "magic_link", "google", "apple" });
		modelBuilder.UseIdentityByDefaultColumns();
		modelBuilder.Entity("Hali.Domain.Entities.Auth.Account", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<int>("AccountType").HasColumnType("integer").HasColumnName("account_type");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<string>("DisplayName").HasMaxLength(120).HasColumnType("character varying(120)")
				.HasColumnName("display_name");
			b.Property<string>("Email").HasMaxLength(254).HasColumnType("character varying(254)")
				.HasColumnName("email");
			b.Property<bool>("IsEmailVerified").HasColumnType("boolean").HasColumnName("is_email_verified");
			b.Property<bool>("IsPhoneVerified").HasColumnType("boolean").HasColumnName("is_phone_verified");
			b.Property<string>("PhoneE164").HasMaxLength(20).HasColumnType("character varying(20)")
				.HasColumnName("phone_e164");
			b.Property<string>("Status").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)")
				.HasColumnName("status");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.HasKey("Id");
			b.HasIndex("Email").IsUnique().HasDatabaseName("uq_accounts_email");
			b.HasIndex("PhoneE164").IsUnique().HasDatabaseName("uq_accounts_phone");
			b.ToTable("accounts", (string?)null);
		});
		modelBuilder.Entity("Hali.Domain.Entities.Auth.Device", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<Guid?>("AccountId").HasColumnType("uuid").HasColumnName("account_id");
			b.Property<string>("AppVersion").HasMaxLength(20).HasColumnType("character varying(20)")
				.HasColumnName("app_version");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<string>("DeviceFingerprintHash").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)")
				.HasColumnName("device_fingerprint_hash");
			b.Property<string>("ExpoPushToken").HasMaxLength(200).HasColumnType("character varying(200)")
				.HasColumnName("expo_push_token");
			b.Property<string>("IntegrityLevel").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)")
				.HasColumnName("integrity_level");
			b.Property<bool>("IsBlocked").HasColumnType("boolean").HasColumnName("is_blocked");
			b.Property<DateTime>("LastSeenAt").HasColumnType("timestamp with time zone").HasColumnName("last_seen_at");
			b.Property<string>("Platform").HasMaxLength(20).HasColumnType("character varying(20)")
				.HasColumnName("platform");
			b.HasKey("Id");
			b.ToTable("devices", (string?)null);
		});
		modelBuilder.Entity("Hali.Domain.Entities.Auth.OtpChallenge", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<Guid?>("AccountId").HasColumnType("uuid").HasColumnName("account_id");
			b.Property<int>("AuthMethod").HasColumnType("integer").HasColumnName("auth_method");
			b.Property<DateTime?>("ConsumedAt").HasColumnType("timestamp with time zone").HasColumnName("consumed_at");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<string>("Destination").IsRequired().HasMaxLength(254)
				.HasColumnType("character varying(254)")
				.HasColumnName("destination");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<string>("OtpHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)")
				.HasColumnName("otp_hash");
			b.HasKey("Id");
			b.ToTable("otp_challenges", (string?)null);
		});
		modelBuilder.Entity("Hali.Domain.Entities.Auth.RefreshToken", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<Guid>("AccountId").HasColumnType("uuid").HasColumnName("account_id");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<Guid?>("DeviceId").HasColumnType("uuid").HasColumnName("device_id");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone").HasColumnName("revoked_at");
			b.Property<string>("TokenHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)")
				.HasColumnName("token_hash");
			b.HasKey("Id");
			b.HasIndex("AccountId").HasDatabaseName("ix_refresh_tokens_account");
			b.HasIndex("DeviceId").HasDatabaseName("ix_refresh_tokens_device");
			b.HasIndex("ExpiresAt").HasDatabaseName("ix_refresh_tokens_expires");
			b.HasIndex("TokenHash").IsUnique().HasDatabaseName("uq_refresh_tokens_hash");
			b.ToTable("refresh_tokens", (string?)null);
		});
	}
}
