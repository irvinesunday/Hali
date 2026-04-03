using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Auth.Migrations;

[DbContext(typeof(AuthDbContext))]
internal class AuthDbContextModelSnapshot : ModelSnapshot
{
	protected override void BuildModel(ModelBuilder modelBuilder)
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
			b.Property<string?>("NotificationSettings").HasColumnType("jsonb").HasColumnName("notification_settings");
			b.Property<Guid?>("InstitutionId").HasColumnType("uuid").HasColumnName("institution_id");
			b.Property<bool>("IsBlocked").HasColumnType("boolean").HasColumnName("is_blocked");
			b.HasKey("Id");
			b.HasIndex("Email").IsUnique().HasDatabaseName("uq_accounts_email");
			b.HasIndex("PhoneE164").IsUnique().HasDatabaseName("uq_accounts_phone");
			b.ToTable("accounts", (string?)null);
		});
		modelBuilder.Entity("Hali.Domain.Entities.Auth.InstitutionInvite", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid").HasColumnName("id");
			b.Property<Guid>("InstitutionId").HasColumnType("uuid").HasColumnName("institution_id");
			b.Property<string>("InviteTokenHash").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)").HasColumnName("invite_token_hash");
			b.Property<Guid>("InvitedByAccountId").HasColumnType("uuid").HasColumnName("invited_by_account_id");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<DateTime?>("AcceptedAt").HasColumnType("timestamp with time zone").HasColumnName("accepted_at");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.HasKey("Id");
			b.HasIndex("InviteTokenHash").IsUnique().HasDatabaseName("ix_institution_invites_token");
			b.HasIndex("InstitutionId").HasDatabaseName("ix_institution_invites_institution");
			b.ToTable("institution_invites", (string?)null);
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
