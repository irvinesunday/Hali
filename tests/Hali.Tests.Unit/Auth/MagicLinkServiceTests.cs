using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Auth;

/// <summary>
/// Unit coverage for <see cref="MagicLinkService"/>: token storage (hash, not
/// plaintext), rate limiting gate, and unknown-email enumeration resistance.
/// All dependencies are in-process fakes — no DB or network.
/// </summary>
public sealed class MagicLinkServiceTests
{
    // -----------------------------------------------------------------------
    // GenerateMagicLink_StoresHashNotPlaintext
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateMagicLink_StoresHashNotPlaintext()
    {
        var repo = new FakeMagicLinkRepo();
        var service = BuildService(repo, alwaysAllow: true);

        MagicLinkIssued issued = await service.IssueAsync("user@example.com", "1.2.3.4", default);

        Assert.False(string.IsNullOrEmpty(issued.PlaintextToken));
        MagicLinkToken saved = Assert.Single(repo.SavedTokens);
        // The plaintext must never appear in the stored hash column.
        Assert.NotEqual(issued.PlaintextToken, saved.TokenHash);
        // Hash must be 64 hex chars (SHA-256).
        Assert.Equal(64, saved.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", saved.TokenHash);
    }

    [Fact]
    public async Task GenerateMagicLink_StoresIpAddress()
    {
        var repo = new FakeMagicLinkRepo();
        var service = BuildService(repo, alwaysAllow: true);

        await service.IssueAsync("user@example.com", "192.168.1.1", default);

        Assert.Equal("192.168.1.1", Assert.Single(repo.SavedTokens).IpAddress);
    }

    // -----------------------------------------------------------------------
    // VerifyMagicLink tests — delegate to ConsumeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task VerifyMagicLink_ValidToken_ReturnsMatchedRow()
    {
        var repo = new FakeMagicLinkRepo();
        var service = BuildService(repo, alwaysAllow: true);

        MagicLinkIssued issued = await service.IssueAsync("user@example.com", null, default);

        // ConsumeAsync returns the row when valid + unconsumed.
        MagicLinkToken? result = await service.ConsumeAsync(issued.PlaintextToken, default);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyMagicLink_InvalidToken_ReturnsNull()
    {
        var repo = new FakeMagicLinkRepo();
        var service = BuildService(repo, alwaysAllow: true);

        MagicLinkToken? result = await service.ConsumeAsync("bogus-token", default);
        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyMagicLink_AlreadyConsumed_ReturnsNull()
    {
        var repo = new FakeMagicLinkRepo { ReturnConsumedToken = true };
        var service = BuildService(repo, alwaysAllow: true);
        MagicLinkIssued issued = await service.IssueAsync("user@example.com", null, default);

        // First consume succeeds (repo returns the token).
        MagicLinkToken? first = await service.ConsumeAsync(issued.PlaintextToken, default);
        Assert.NotNull(first);
        // Second consume fails (repo simulates consumed → returns null).
        MagicLinkToken? second = await service.ConsumeAsync(issued.PlaintextToken, default);
        Assert.Null(second);
    }

    [Fact]
    public async Task VerifyMagicLink_ExpiredToken_ReturnsNull()
    {
        var repo = new FakeMagicLinkRepo { ReturnExpiredToken = true };
        var service = BuildService(repo, alwaysAllow: true);
        MagicLinkIssued issued = await service.IssueAsync("user@example.com", null, default);

        MagicLinkToken? result = await service.ConsumeAsync(issued.PlaintextToken, default);
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // Rate limiting
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestMagicLink_ExceedsRateLimit_ThrowsRateLimitException()
    {
        var repo = new FakeMagicLinkRepo();
        var service = BuildService(repo, alwaysAllow: false);

        await Assert.ThrowsAsync<RateLimitException>(() =>
            service.IssueAsync("user@example.com", null, default));
    }

    [Fact]
    public async Task RequestMagicLink_UnknownEmail_ResponseIdenticalToSuccess()
    {
        // IssueAsync should succeed (not throw) even when the email is not
        // registered — the caller must receive an identical response shape.
        var repo = new FakeMagicLinkRepo { KnownEmail = null }; // unknown email
        var service = BuildService(repo, alwaysAllow: true, knownEmail: null);

        // Must not throw; issued token shape is the same as for a known email.
        MagicLinkIssued issued = await service.IssueAsync("nobody@example.com", null, default);
        Assert.False(string.IsNullOrEmpty(issued.PlaintextToken));
        Assert.True(issued.ExpiresAt > DateTime.UtcNow);

        // The row is still written (with AccountId = null).
        MagicLinkToken saved = Assert.Single(repo.SavedTokens);
        Assert.Null(saved.AccountId);
    }

    // -----------------------------------------------------------------------
    // HashToken is deterministic and produces a 64-char hex SHA-256
    // -----------------------------------------------------------------------

    [Fact]
    public void HashToken_IsDeterministicAndProduces64HexChars()
    {
        var service = BuildService(new FakeMagicLinkRepo(), alwaysAllow: true);
        string hash1 = service.HashToken("some-token-value");
        string hash2 = service.HashToken("some-token-value");
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    // -----------------------------------------------------------------------
    // Audit events are emitted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkRequested_EmitsAuditEvent()
    {
        var repo = new FakeMagicLinkRepo();
        var audit = new FakeAuditService();
        var service = BuildService(repo, alwaysAllow: true, audit: audit);

        await service.IssueAsync("user@example.com", "1.2.3.4", default);

        Assert.Contains(AuthAuditEvents.MagicLinkRequested, audit.LoggedEvents);
    }

    [Fact]
    public async Task MagicLinkRateLimitHit_EmitsAuditEvent()
    {
        var repo = new FakeMagicLinkRepo();
        var audit = new FakeAuditService();
        var service = BuildService(repo, alwaysAllow: false, audit: audit);

        await Assert.ThrowsAsync<RateLimitException>(() =>
            service.IssueAsync("user@example.com", "1.2.3.4", default));

        Assert.Contains(AuthAuditEvents.MagicLinkRateLimitHit, audit.LoggedEvents);
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private static MagicLinkService BuildService(
        FakeMagicLinkRepo repo,
        bool alwaysAllow,
        string? knownEmail = "user@example.com",
        FakeAuditService? audit = null)
    {
        var authRepo = new FakeAuthRepo(knownEmail);
        var emailSender = new FakeEmailSender();
        var rateLimiter = new FakeRateLimiter(alwaysAllow);
        var auditSvc = audit ?? new FakeAuditService();
        var authOpts = Options.Create(new AuthOptions { AppBaseUrl = "https://hali.example.com" });
        var instOpts = Options.Create(new InstitutionAuthOptions { MagicLinkTtlMinutes = 15 });
        return new MagicLinkService(
            repo, authRepo, emailSender, rateLimiter, auditSvc,
            authOpts, instOpts, NullLogger<MagicLinkService>.Instance);
    }

    // ======================================================================
    // In-process fakes
    // ======================================================================

    private sealed class FakeMagicLinkRepo : IInstitutionAuthRepository
    {
        public List<MagicLinkToken> SavedTokens { get; } = new();
        public bool ReturnConsumedToken { get; set; }
        public bool ReturnExpiredToken { get; set; }
        public string? KnownEmail { get; set; } = "user@example.com";

        private bool _consumed;

        public Task SaveMagicLinkAsync(MagicLinkToken token, CancellationToken ct)
        {
            SavedTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<MagicLinkToken?> ConsumeMagicLinkAsync(
            string tokenHash, DateTime now, CancellationToken ct)
        {
            if (ReturnExpiredToken) return Task.FromResult<MagicLinkToken?>(null);
            if (ReturnConsumedToken)
            {
                if (_consumed) return Task.FromResult<MagicLinkToken?>(null);
                _consumed = true;
                var t = SavedTokens.Find(x => x.TokenHash == tokenHash);
                return Task.FromResult(t);
            }
            var match = SavedTokens.Find(x => x.TokenHash == tokenHash);
            return Task.FromResult(match);
        }

        // Unused by these tests — not implemented
        public Task<TotpSecret?> FindTotpSecretByAccountAsync(Guid accountId, CancellationToken ct)
            => throw new NotImplementedException();
        public Task SaveTotpSecretAsync(TotpSecret secret, CancellationToken ct)
            => throw new NotImplementedException();
        public Task UpdateTotpSecretAsync(TotpSecret secret, CancellationToken ct)
            => throw new NotImplementedException();
        public Task DeleteRecoveryCodesForAccountAsync(Guid accountId, CancellationToken ct)
            => throw new NotImplementedException();
        public Task ConfirmTotpSecretAsync(Guid totpSecretId, DateTime confirmedAt, CancellationToken ct)
            => throw new NotImplementedException();
        public Task SaveRecoveryCodesAsync(IEnumerable<TotpRecoveryCode> codes, CancellationToken ct)
            => throw new NotImplementedException();
        public Task<bool> ConsumeRecoveryCodeAsync(Guid accountId, string codeHash, DateTime usedAt, CancellationToken ct)
            => throw new NotImplementedException();
        public Task SaveWebSessionAsync(WebSession session, CancellationToken ct)
            => throw new NotImplementedException();
        public Task<WebSession?> FindActiveWebSessionAsync(string sessionTokenHash, DateTime now, CancellationToken ct)
            => throw new NotImplementedException();
        public Task TouchWebSessionAsync(Guid sessionId, DateTime lastActivityAt, CancellationToken ct)
            => throw new NotImplementedException();
        public Task RevokeWebSessionAsync(Guid sessionId, DateTime revokedAt, CancellationToken ct)
            => throw new NotImplementedException();
        public Task MarkStepUpVerifiedAsync(Guid sessionId, DateTime verifiedAt, CancellationToken ct)
            => throw new NotImplementedException();
    }

    private sealed class FakeAuthRepo : IAuthRepository
    {
        private readonly Account? _account;

        public FakeAuthRepo(string? knownEmail)
        {
            _account = knownEmail is null ? null : new Account
            {
                Id = Guid.NewGuid(),
                Email = knownEmail,
                AccountType = Domain.Enums.AccountType.InstitutionUser,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        public Task<Account?> FindAccountByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(_account?.Email == email ? _account : null);

        public Task<Account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct)
            => Task.FromResult(_account?.Id == accountId ? _account : null);

        // Remainder not used by these tests.
        public Task<Account?> FindAccountByPhoneAsync(string phoneE164, CancellationToken ct) => throw new NotImplementedException();
        public Task<Account> CreateAccountAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveOtpChallengeAsync(OtpChallenge challenge, CancellationToken ct) => throw new NotImplementedException();
        public Task<OtpChallenge?> FindActiveOtpAsync(string destination, DateTime now, CancellationToken ct) => throw new NotImplementedException();
        public Task ConsumeOtpAsync(OtpChallenge challenge, DateTime consumedAt, CancellationToken ct) => throw new NotImplementedException();
        public Task<Device> UpsertDeviceAsync(string fingerprintHash, Guid accountId, string? platform, string? appVersion, string? expoPushToken, DateTime now, CancellationToken ct) => throw new NotImplementedException();
        public Task<RefreshToken?> FindActiveRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct) => throw new NotImplementedException();
        public Task RevokeRefreshTokenAsync(RefreshToken token, DateTime revokedAt, CancellationToken ct) => throw new NotImplementedException();
        public Task<Device?> FindDeviceByFingerprintAsync(string fingerprintHash, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateExpoPushTokenAsync(Guid deviceId, string expoPushToken, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateAccountAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetPushTokensByAccountIdsAsync(IEnumerable<Guid> accountIds, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeEmailSender : IInstitutionEmailSender
    {
        public Task SendMagicLinkAsync(string destinationEmail, string url, DateTime expiresAt, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeRateLimiter : IRateLimiter
    {
        private readonly bool _allow;
        public FakeRateLimiter(bool allow) => _allow = allow;
        public Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken ct)
            => Task.FromResult(_allow);
    }

    private sealed class FakeAuditService : IAuthAuditService
    {
        public List<string> LoggedEvents { get; } = new();
        public Task LogAsync(string eventType, string? ipAddress, CancellationToken ct = default)
        {
            LoggedEvents.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
