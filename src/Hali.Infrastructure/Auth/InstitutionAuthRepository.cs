using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Auth;

public sealed class InstitutionAuthRepository : IInstitutionAuthRepository
{
    private readonly AuthDbContext _db;

    public InstitutionAuthRepository(AuthDbContext db)
    {
        _db = db;
    }

    // ---- Magic link -------------------------------------------------------

    public async Task SaveMagicLinkAsync(MagicLinkToken token, CancellationToken ct)
    {
        _db.MagicLinkTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MagicLinkToken?> ConsumeMagicLinkAsync(
        string tokenHash, DateTime now, CancellationToken ct)
    {
        // Atomic test-and-set — EF generates a WHERE-guarded UPDATE so a
        // re-presented token cannot be consumed twice even under
        // concurrent verify requests. A zero-row result means the token
        // was unknown, expired, or already consumed — we deliberately do
        // not surface the cause to the wire.
        int updated = await _db.MagicLinkTokens
            .Where(t => t.TokenHash == tokenHash
                     && t.ConsumedAt == null
                     && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);

        if (updated == 0)
        {
            return null;
        }

        return await _db.MagicLinkTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    // ---- TOTP -------------------------------------------------------------

    public Task<TotpSecret?> FindTotpSecretByAccountAsync(Guid accountId, CancellationToken ct)
        => _db.TotpSecrets.FirstOrDefaultAsync(s => s.AccountId == accountId && s.RevokedAt == null, ct);

    public async Task SaveTotpSecretAsync(TotpSecret secret, CancellationToken ct)
    {
        _db.TotpSecrets.Add(secret);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ConfirmTotpSecretAsync(Guid totpSecretId, DateTime confirmedAt, CancellationToken ct)
    {
        await _db.TotpSecrets
            .Where(s => s.Id == totpSecretId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConfirmedAt, confirmedAt), ct);
    }

    public async Task SaveRecoveryCodesAsync(
        IEnumerable<TotpRecoveryCode> codes, CancellationToken ct)
    {
        _db.TotpRecoveryCodes.AddRange(codes);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ConsumeRecoveryCodeAsync(
        Guid accountId, string codeHash, DateTime usedAt, CancellationToken ct)
    {
        int updated = await _db.TotpRecoveryCodes
            .Where(c => c.AccountId == accountId && c.CodeHash == codeHash && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, usedAt), ct);
        return updated > 0;
    }

    // ---- Web sessions -----------------------------------------------------

    public async Task SaveWebSessionAsync(WebSession session, CancellationToken ct)
    {
        _db.WebSessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public Task<WebSession?> FindActiveWebSessionAsync(string sessionTokenHash, DateTime now, CancellationToken ct)
        => _db.WebSessions.FirstOrDefaultAsync(
            s => s.SessionTokenHash == sessionTokenHash
                && s.RevokedAt == null
                && s.AbsoluteExpiresAt > now,
            ct);

    public async Task TouchWebSessionAsync(Guid sessionId, DateTime lastActivityAt, CancellationToken ct)
    {
        await _db.WebSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(ws => ws.LastActivityAt, lastActivityAt), ct);
    }

    public async Task RevokeWebSessionAsync(Guid sessionId, DateTime revokedAt, CancellationToken ct)
    {
        await _db.WebSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(ws => ws.RevokedAt, revokedAt), ct);
    }

    public async Task MarkStepUpVerifiedAsync(Guid sessionId, DateTime verifiedAt, CancellationToken ct)
    {
        await _db.WebSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(ws => ws.StepUpVerifiedAt, verifiedAt), ct);
    }
}
