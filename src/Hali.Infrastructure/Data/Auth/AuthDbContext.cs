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

	public DbSet<InstitutionInvite> InstitutionInvites => Set<InstitutionInvite>();

	public DbSet<WebSession> WebSessions => Set<WebSession>();

	public DbSet<TotpSecret> TotpSecrets => Set<TotpSecret>();

	public DbSet<TotpRecoveryCode> TotpRecoveryCodes => Set<TotpRecoveryCode>();

	public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

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
			e.Property((Account x) => x.InstitutionId).HasColumnName("institution_id");
			e.Property((Account x) => x.IsBlocked).HasColumnName("is_blocked");
			e.HasIndex((Account x) => x.Email).IsUnique().HasDatabaseName("uq_accounts_email");
			e.HasIndex((Account x) => x.PhoneE164).IsUnique().HasDatabaseName("uq_accounts_phone");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<InstitutionInvite> e)
		{
			e.ToTable("institution_invites");
			e.HasKey((InstitutionInvite x) => x.Id);
			e.Property((InstitutionInvite x) => x.Id).HasColumnName("id");
			e.Property((InstitutionInvite x) => x.InstitutionId).HasColumnName("institution_id");
			e.Property((InstitutionInvite x) => x.InviteTokenHash).HasColumnName("invite_token_hash").HasMaxLength(64);
			e.Property((InstitutionInvite x) => x.InvitedByAccountId).HasColumnName("invited_by_account_id");
			e.Property((InstitutionInvite x) => x.ExpiresAt).HasColumnName("expires_at");
			e.Property((InstitutionInvite x) => x.AcceptedAt).HasColumnName("accepted_at");
			e.Property((InstitutionInvite x) => x.CreatedAt).HasColumnName("created_at");
			e.HasIndex((InstitutionInvite x) => x.InviteTokenHash).IsUnique().HasDatabaseName("ix_institution_invites_token");
			e.HasIndex((InstitutionInvite x) => x.InstitutionId).HasDatabaseName("ix_institution_invites_institution");
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
		modelBuilder.Entity(delegate(EntityTypeBuilder<WebSession> e)
		{
			e.ToTable("web_sessions");
			e.HasKey((WebSession x) => x.Id);
			e.Property((WebSession x) => x.Id).HasColumnName("id");
			e.Property((WebSession x) => x.AccountId).HasColumnName("account_id");
			e.Property((WebSession x) => x.InstitutionId).HasColumnName("institution_id");
			e.Property((WebSession x) => x.SessionTokenHash).HasColumnName("session_token_hash").HasMaxLength(128);
			e.Property((WebSession x) => x.CsrfTokenHash).HasColumnName("csrf_token_hash").HasMaxLength(128);
			e.Property((WebSession x) => x.CreatedAt).HasColumnName("created_at");
			e.Property((WebSession x) => x.LastActivityAt).HasColumnName("last_activity_at");
			e.Property((WebSession x) => x.AbsoluteExpiresAt).HasColumnName("absolute_expires_at");
			e.Property((WebSession x) => x.StepUpVerifiedAt).HasColumnName("step_up_verified_at");
			e.Property((WebSession x) => x.RevokedAt).HasColumnName("revoked_at");
			e.HasIndex((WebSession x) => x.SessionTokenHash).IsUnique().HasDatabaseName("uq_web_sessions_token");
			e.HasIndex((WebSession x) => x.AccountId).HasDatabaseName("ix_web_sessions_account");
			e.HasIndex((WebSession x) => x.AbsoluteExpiresAt).HasDatabaseName("ix_web_sessions_absolute_expires");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<TotpSecret> e)
		{
			e.ToTable("totp_secrets");
			e.HasKey((TotpSecret x) => x.Id);
			e.Property((TotpSecret x) => x.Id).HasColumnName("id");
			e.Property((TotpSecret x) => x.AccountId).HasColumnName("account_id");
			e.Property((TotpSecret x) => x.SecretEncrypted).HasColumnName("secret_encrypted");
			e.Property((TotpSecret x) => x.EnrolledAt).HasColumnName("enrolled_at");
			e.Property((TotpSecret x) => x.ConfirmedAt).HasColumnName("confirmed_at");
			e.Property((TotpSecret x) => x.RevokedAt).HasColumnName("revoked_at");
			e.HasIndex((TotpSecret x) => x.AccountId).IsUnique().HasDatabaseName("uq_totp_secrets_account");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<TotpRecoveryCode> e)
		{
			e.ToTable("totp_recovery_codes");
			e.HasKey((TotpRecoveryCode x) => x.Id);
			e.Property((TotpRecoveryCode x) => x.Id).HasColumnName("id");
			e.Property((TotpRecoveryCode x) => x.AccountId).HasColumnName("account_id");
			e.Property((TotpRecoveryCode x) => x.CodeHash).HasColumnName("code_hash").HasMaxLength(128);
			e.Property((TotpRecoveryCode x) => x.UsedAt).HasColumnName("used_at");
			e.Property((TotpRecoveryCode x) => x.CreatedAt).HasColumnName("created_at");
			e.HasIndex((TotpRecoveryCode x) => new { x.AccountId, x.CodeHash }).IsUnique().HasDatabaseName("uq_totp_recovery_codes");
		});
		modelBuilder.Entity(delegate(EntityTypeBuilder<MagicLinkToken> e)
		{
			e.ToTable("magic_link_tokens");
			e.HasKey((MagicLinkToken x) => x.Id);
			e.Property((MagicLinkToken x) => x.Id).HasColumnName("id");
			e.Property((MagicLinkToken x) => x.DestinationEmail).HasColumnName("destination_email").HasMaxLength(254);
			e.Property((MagicLinkToken x) => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
			e.Property((MagicLinkToken x) => x.AccountId).HasColumnName("account_id");
			e.Property((MagicLinkToken x) => x.ExpiresAt).HasColumnName("expires_at");
			e.Property((MagicLinkToken x) => x.ConsumedAt).HasColumnName("consumed_at");
			e.Property((MagicLinkToken x) => x.CreatedAt).HasColumnName("created_at");
			e.HasIndex((MagicLinkToken x) => x.TokenHash).IsUnique().HasDatabaseName("uq_magic_link_tokens_hash");
			e.HasIndex((MagicLinkToken x) => x.DestinationEmail).HasDatabaseName("ix_magic_link_tokens_email");
			e.HasIndex((MagicLinkToken x) => x.ExpiresAt).HasDatabaseName("ix_magic_link_tokens_expires");
		});
	}
}
