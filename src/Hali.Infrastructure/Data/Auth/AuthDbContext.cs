using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Data.Auth;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AccountType>("account_type");
        modelBuilder.HasPostgresEnum<AuthMethod>("auth_method");

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountType).HasColumnName("account_type");
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
            e.Property(x => x.PhoneE164).HasColumnName("phone_e164").HasMaxLength(20);
            e.Property(x => x.IsPhoneVerified).HasColumnName("is_phone_verified");
            e.Property(x => x.IsEmailVerified).HasColumnName("is_email_verified");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_accounts_email");
            e.HasIndex(x => x.PhoneE164).IsUnique().HasDatabaseName("uq_accounts_phone");
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.DeviceFingerprintHash).HasColumnName("device_fingerprint_hash").HasMaxLength(128);
            e.Property(x => x.IntegrityLevel).HasColumnName("device_integrity_level").HasMaxLength(30);
            e.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(30);
            e.Property(x => x.AppVersion).HasColumnName("app_version").HasMaxLength(30);
            e.Property(x => x.ExpoPushToken).HasColumnName("expo_push_token").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("first_seen_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            e.Property(x => x.IsBlocked).HasColumnName("is_blocked");
        });

        modelBuilder.Entity<OtpChallenge>(e =>
        {
            e.ToTable("otp_challenges");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.AuthMethod).HasColumnName("auth_method");
            e.Property(x => x.Destination).HasColumnName("destination").HasMaxLength(254);
            e.Property(x => x.OtpHash).HasColumnName("otp_hash").HasMaxLength(128);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_hash");
            e.HasIndex(x => x.AccountId).HasDatabaseName("ix_refresh_tokens_account");
            e.HasIndex(x => x.DeviceId).HasDatabaseName("ix_refresh_tokens_device");
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires");
        });
    }
}
