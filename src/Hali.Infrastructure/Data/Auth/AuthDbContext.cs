using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hali.Infrastructure.Data.Auth;

public class AuthDbContext : DbContext
{
	public DbSet<Account> Accounts => Set<Account>();

	public DbSet<Device> Devices => Set<Device>();

	public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

	public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

	public AuthDbContext(DbContextOptions<AuthDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasPostgresEnum<AccountType>("account_type");
		modelBuilder.HasPostgresEnum<AuthMethod>("auth_method");
		modelBuilder.Entity(delegate(EntityTypeBuilder<Account> e)
		{
			e.ToTable("accounts");
			e.HasKey((Account x) => x.Id);
			e.Property((Account x) => x.Id).HasColumnName("id");
			e.Property((Account x) => x.AccountType).HasColumnName("account_type");
			e.Property((Account x) => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
			e.Property((Account x) => x.Email).HasColumnName("email").HasMaxLength(254);
			e.Property((Account x) => x.PhoneE164).HasColumnName("phone_e164").HasMaxLength(20);
			e.Property((Account x) => x.IsPhoneVerified).HasColumnName("is_phone_verified");
			e.Property((Account x) => x.IsEmailVerified).HasColumnName("is_email_verified");
			e.Property((Account x) => x.Status).HasColumnName("status").HasMaxLength(20);
			e.Property((Account x) => x.CreatedAt).HasColumnName("created_at");
			e.Property((Account x) => x.UpdatedAt).HasColumnName("updated_at");
			e.Property((Account x) => x.NotificationSettings).HasColumnName("notification_settings").HasColumnType("jsonb");
			e.HasIndex((Account x) => x.Email).IsUnique().HasDatabaseName("uq_accounts_email");
			e.HasIndex((Account x) => x.PhoneE164).IsUnique().HasDatabaseName("uq_accounts_phone");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<Device> e)
		{
			e.ToTable("devices");
			e.HasKey((Device x) => x.Id);
			e.Property((Device x) => x.Id).HasColumnName("id");
			e.Property((Device x) => x.AccountId).HasColumnName("account_id");
			e.Property((Device x) => x.DeviceFingerprintHash).HasColumnName("device_fingerprint_hash").HasMaxLength(128);
			e.Property((Device x) => x.IntegrityLevel).HasColumnName("device_integrity_level").HasMaxLength(30);
			e.Property((Device x) => x.Platform).HasColumnName("platform").HasMaxLength(30);
			e.Property((Device x) => x.AppVersion).HasColumnName("app_version").HasMaxLength(30);
			e.Property((Device x) => x.ExpoPushToken).HasColumnName("expo_push_token").HasMaxLength(200);
			e.Property((Device x) => x.CreatedAt).HasColumnName("first_seen_at");
			e.Property((Device x) => x.LastSeenAt).HasColumnName("last_seen_at");
			e.Property((Device x) => x.IsBlocked).HasColumnName("is_blocked");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<OtpChallenge> e)
		{
			e.ToTable("otp_challenges");
			e.HasKey((OtpChallenge x) => x.Id);
			e.Property((OtpChallenge x) => x.Id).HasColumnName("id");
			e.Property((OtpChallenge x) => x.AccountId).HasColumnName("account_id");
			e.Property((OtpChallenge x) => x.AuthMethod).HasColumnName("auth_method");
			e.Property((OtpChallenge x) => x.Destination).HasColumnName("destination").HasMaxLength(254);
			e.Property((OtpChallenge x) => x.OtpHash).HasColumnName("otp_hash").HasMaxLength(128);
			e.Property((OtpChallenge x) => x.ExpiresAt).HasColumnName("expires_at");
			e.Property((OtpChallenge x) => x.ConsumedAt).HasColumnName("consumed_at");
			e.Property((OtpChallenge x) => x.CreatedAt).HasColumnName("created_at");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<RefreshToken> e)
		{
			e.ToTable("refresh_tokens");
			e.HasKey((RefreshToken x) => x.Id);
			e.Property((RefreshToken x) => x.Id).HasColumnName("id");
			e.Property((RefreshToken x) => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
			e.Property((RefreshToken x) => x.AccountId).HasColumnName("account_id");
			e.Property((RefreshToken x) => x.DeviceId).HasColumnName("device_id");
			e.Property((RefreshToken x) => x.CreatedAt).HasColumnName("created_at");
			e.Property((RefreshToken x) => x.ExpiresAt).HasColumnName("expires_at");
			e.Property((RefreshToken x) => x.RevokedAt).HasColumnName("revoked_at");
			e.HasIndex((RefreshToken x) => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_hash");
			e.HasIndex((RefreshToken x) => x.AccountId).HasDatabaseName("ix_refresh_tokens_account");
			e.HasIndex((RefreshToken x) => x.DeviceId).HasDatabaseName("ix_refresh_tokens_device");
			e.HasIndex((RefreshToken x) => x.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires");
		});
	}
}
