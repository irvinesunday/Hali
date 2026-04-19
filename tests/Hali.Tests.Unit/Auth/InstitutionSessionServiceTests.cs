using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Auth;

/// <summary>
/// Unit coverage for <see cref="InstitutionSessionService"/>: session token
/// storage (hash not plaintext), idle + absolute timeout enforcement, revoked
/// session rejection, and the TouchAsync sliding-window update.
/// </summary>
public sealed class InstitutionSessionServiceTests
{
    // -----------------------------------------------------------------------
    // CreateAsync — token storage
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateSession_StoresHashNotPlaintext()
    {
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);

        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);

        Assert.False(string.IsNullOrEmpty(created.SessionTokenPlaintext));
        WebSession saved = Assert.Single(repo.SavedSessions);
        // Plaintext must not be stored verbatim.
        Assert.NotEqual(created.SessionTokenPlaintext, saved.SessionTokenHash);
        // Hash must be 64 hex chars (SHA-256).
        Assert.Equal(64, saved.SessionTokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", saved.SessionTokenHash);

        // CSRF token obeys the same rule.
        Assert.NotEqual(created.CsrfTokenPlaintext, saved.CsrfTokenHash);
        Assert.Equal(64, saved.CsrfTokenHash.Length);
    }

    [Fact]
    public async Task CreateSession_PlaintextTokenNotReusable()
    {
        // Two separate CreateAsync calls must yield distinct plaintexts.
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);
        var id = Guid.NewGuid();

        SessionCreated a = await service.CreateAsync(id, null, "institution", default);
        SessionCreated b = await service.CreateAsync(id, null, "institution", default);

        Assert.NotEqual(a.SessionTokenPlaintext, b.SessionTokenPlaintext);
        Assert.NotEqual(a.CsrfTokenPlaintext, b.CsrfTokenPlaintext);
    }

    // -----------------------------------------------------------------------
    // ValidateAsync — active session
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValidateSession_ActiveSession_ReturnsOk()
    {
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);
        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);

        SessionValidation result = await service.ValidateAsync(created.SessionTokenPlaintext, default);

        Assert.Equal(SessionValidationResult.Ok, result.Result);
        Assert.NotNull(result.Session);
    }

    // -----------------------------------------------------------------------
    // ValidateAsync — session not found / revoked
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValidateSession_UnknownToken_ReturnsInvalid()
    {
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);

        SessionValidation result = await service.ValidateAsync("not-a-real-token", default);

        Assert.Equal(SessionValidationResult.Invalid, result.Result);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task ValidateSession_RevokedSession_ReturnsInvalid()
    {
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);
        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);
        await service.RevokeAsync(created.Session.Id, default);

        // Repo returns null for revoked sessions.
        SessionValidation result = await service.ValidateAsync(created.SessionTokenPlaintext, default);
        Assert.Equal(SessionValidationResult.Invalid, result.Result);
    }

    // -----------------------------------------------------------------------
    // ValidateAsync — timeout enforcement
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValidateSession_IdleTimeout_ReturnsIdleTimeout()
    {
        var repo = new FakeSessionRepo();
        // Idle threshold is 30 min — put last_activity 31 min in the past.
        repo.LastActivityOverride = DateTime.UtcNow.AddMinutes(-31);
        var service = BuildService(repo, idleMinutes: 30, absoluteHours: 12);
        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);

        SessionValidation result = await service.ValidateAsync(created.SessionTokenPlaintext, default);
        Assert.Equal(SessionValidationResult.IdleTimeout, result.Result);
    }

    [Fact]
    public async Task HardExpiry_EnforcedRegardlessOfActivity()
    {
        // Even a "recent" last_activity cannot save a session past its
        // absolute_expires_at.
        var repo = new FakeSessionRepo();
        repo.AbsoluteExpiresAtOverride = DateTime.UtcNow.AddHours(-1); // expired 1 h ago
        repo.LastActivityOverride = DateTime.UtcNow.AddSeconds(-5);   // active very recently
        var service = BuildService(repo, idleMinutes: 30, absoluteHours: 12);
        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);

        SessionValidation result = await service.ValidateAsync(created.SessionTokenPlaintext, default);
        Assert.Equal(SessionValidationResult.AbsoluteTimeout, result.Result);
    }

    // -----------------------------------------------------------------------
    // TouchAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TouchSession_UpdatesLastActivityAt()
    {
        var repo = new FakeSessionRepo();
        var service = BuildService(repo);
        SessionCreated created = await service.CreateAsync(Guid.NewGuid(), null, "institution", default);
        DateTime before = repo.SavedSessions[0].LastActivityAt;

        await service.TouchAsync(created.Session.Id, default);

        Assert.True(repo.LastTouchedAt >= before);
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private static InstitutionSessionService BuildService(
        FakeSessionRepo repo,
        int idleMinutes = 30,
        int absoluteHours = 12)
    {
        var opts = Options.Create(new InstitutionAuthOptions
        {
            SessionIdleMinutes = idleMinutes,
            SessionAbsoluteHours = absoluteHours,
        });
        return new InstitutionSessionService(repo, opts);
    }

    // ======================================================================
    // In-process fake
    // ======================================================================

    private sealed class FakeSessionRepo : IInstitutionAuthRepository
    {
        public List<WebSession> SavedSessions { get; } = new();
        public DateTime? LastActivityOverride { get; set; }
        public DateTime? AbsoluteExpiresAtOverride { get; set; }
        public DateTime LastTouchedAt { get; private set; }
        private readonly HashSet<Guid> _revoked = new();

        public Task SaveWebSessionAsync(WebSession session, CancellationToken ct)
        {
            // Apply overrides so tests can manipulate timestamps.
            if (LastActivityOverride.HasValue)
                session.LastActivityAt = LastActivityOverride.Value;
            if (AbsoluteExpiresAtOverride.HasValue)
                session.AbsoluteExpiresAt = AbsoluteExpiresAtOverride.Value;
            SavedSessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<WebSession?> FindActiveWebSessionAsync(
            string sessionTokenHash, DateTime now, CancellationToken ct)
        {
            foreach (var s in SavedSessions)
            {
                if (s.SessionTokenHash == sessionTokenHash && !_revoked.Contains(s.Id))
                    return Task.FromResult<WebSession?>(s);
            }
            return Task.FromResult<WebSession?>(null);
        }

        public Task TouchWebSessionAsync(Guid sessionId, DateTime lastActivityAt, CancellationToken ct)
        {
            LastTouchedAt = lastActivityAt;
            return Task.CompletedTask;
        }

        public Task RevokeWebSessionAsync(Guid sessionId, DateTime revokedAt, CancellationToken ct)
        {
            _revoked.Add(sessionId);
            return Task.CompletedTask;
        }

        public Task MarkStepUpVerifiedAsync(Guid sessionId, DateTime verifiedAt, CancellationToken ct)
            => Task.CompletedTask;

        // Unused by session tests.
        public Task SaveMagicLinkAsync(MagicLinkToken token, CancellationToken ct) => throw new NotImplementedException();
        public Task<MagicLinkToken?> ConsumeMagicLinkAsync(string tokenHash, DateTime now, CancellationToken ct) => throw new NotImplementedException();
        public Task<TotpSecret?> FindTotpSecretByAccountAsync(Guid accountId, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveTotpSecretAsync(TotpSecret secret, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateTotpSecretAsync(TotpSecret secret, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteRecoveryCodesForAccountAsync(Guid accountId, CancellationToken ct) => throw new NotImplementedException();
        public Task ConfirmTotpSecretAsync(Guid totpSecretId, DateTime confirmedAt, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveRecoveryCodesAsync(IEnumerable<TotpRecoveryCode> codes, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ConsumeRecoveryCodeAsync(Guid accountId, string codeHash, DateTime usedAt, CancellationToken ct) => throw new NotImplementedException();
    }
}
