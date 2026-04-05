# Hali — Auth Implementation Guide
**Authentication flows, token strategy, and session management for all surfaces.**

---

## Surface-to-auth mapping

| Surface | Auth method | 2FA | Token storage |
|---|---|---|---|
| Citizen Mobile | SMS OTP (Africa's Talking) | None | Expo SecureStore |
| Institution Ops Dashboard | Email magic link | TOTP required | httpOnly cookie |
| Institution Admin Dashboard | Email magic link | TOTP required | httpOnly cookie |
| Hali Ops Dashboard | Email magic link | TOTP required | httpOnly cookie |

---

## Phase 1 — Citizen SMS OTP flow

### Sequence

```
1. POST /v1/auth/otp
   Body: { method: "phone_otp", destination: "+254712345678" }
   
   Server:
   - Rate limit check: max 3 requests per destination per 10 min (Redis: rl:otp:{destination})
   - Look up or create account by phone_e164
   - Generate 6-digit OTP
   - Hash OTP with bcrypt (cost 10)
   - Insert otp_challenges row: { account_id, auth_method, destination, otp_hash, expires_at: +10min }
   - Send OTP via Africa's Talking ISmsProvider
   
   Response 202: { challengeId, expiresAt }

2. POST /v1/auth/verify
   Body: { challengeId, otpCode, deviceFingerprint }
   
   Server:
   - Fetch otp_challenges by id
   - Check: not expired, not consumed, attempt count < 3
   - Verify bcrypt hash
   - Mark challenge consumed_at = now()
   - Upsert device record (fingerprint hash → device row)
   - Issue access token (JWT, 60 min)
   - Issue refresh token (random 32 bytes → base64 → store SHA-256 hash)
   - Insert refresh_tokens row
   
   Response 200: { accessToken, refreshToken, expiresIn: 3600, account: {...} }

3. Mobile stores:
   - accessToken in memory
   - refreshToken in Expo SecureStore (never localStorage, never AsyncStorage unencrypted)
```

### Token generation

```csharp
// Access token
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
    new Claim("account_type", account.AccountType.ToString()),
    new Claim("device_id", device.Id.ToString()),
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
};
// Sign with HS256, audience: "hali-platform", issuer: "hali-api"

// Refresh token
var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
// Store tokenHash in refresh_tokens; return rawToken to client
```

---

## Phase 2 — Web surface auth (magic link + TOTP)

### New routes required (Phase 2)

```
POST /v1/auth/otp          (existing — add magic_link method support)
GET  /v1/auth/magic        ?token={token}   — magic link redemption
POST /v1/auth/totp/setup   — enroll TOTP device, returns QR code data
POST /v1/auth/totp/verify  — verify TOTP code during login
POST /v1/auth/refresh      (existing — add cookie support via X-Surface header)
POST /v1/auth/logout       (existing — invalidate cookie on web)
```

### Magic link flow

```
1. POST /v1/auth/otp
   Body: { method: "magic_link", destination: "user@kenyapower.co.ke" }
   
   Server:
   - Rate limit check: 3 requests per destination per 10 min
   - Look up account by email — 404 if not found (web users pre-created by institution admin)
   - Generate magic link token: 32 random bytes → base64url
   - Store in otp_challenges: { auth_method: magic_link, destination: email, otp_hash: bcrypt(token), expires_at: +15min }
   - Send email with link: https://institution.hali.app/auth/magic?token={token}&challengeId={id}
   
   Response 202: { challengeId, message: "Check your email" }

2. User clicks link → GET /v1/auth/magic?token=...&challengeId=...
   
   Server:
   - Validate token (same bcrypt check as OTP)
   - If account has totp_secret: redirect to /auth/totp with { pendingChallengeId }
   - If no totp enrolled (first login): redirect to /auth/totp/setup with { pendingChallengeId }
   - Never issue tokens before 2FA is complete

3. POST /v1/auth/totp/setup
   Body: { pendingChallengeId }
   Server: Generate TOTP secret, return { qrCodeDataUrl, secret, pendingChallengeId }
   Client: Shows QR code — user scans with authenticator app

4. POST /v1/auth/totp/verify
   Body: { pendingChallengeId, totpCode }
   Server:
   - Validate TOTP code against stored secret (allow ±1 time window)
   - If setup flow: save totp_secret to account, mark enrolled
   - Issue access token + refresh token
   - Set httpOnly cookie (web) OR return in body (mobile — but web users always use cookie)
   
   Response 200: tokens (web: cookie; never in body for web requests)
```

### Web cookie strategy

```csharp
// Detect surface via X-Surface header
// X-Surface: web → set cookie
// X-Surface: mobile (or absent) → return in body

if (request.Headers["X-Surface"] == "web")
{
    response.Cookies.Append("hali_session", accessToken, new CookieOptions
    {
        HttpOnly    = true,
        Secure      = true,
        SameSite    = SameSiteMode.Strict,
        Domain      = ".hali.app",
        Expires     = DateTimeOffset.UtcNow.AddMinutes(60)
    });
    response.Cookies.Append("hali_refresh", refreshToken, new CookieOptions
    {
        HttpOnly    = true,
        Secure      = true,
        SameSite    = SameSiteMode.Strict,
        Path        = "/v1/auth/refresh",  // restrict refresh token cookie to refresh endpoint
        Domain      = ".hali.app",
        Expires     = DateTimeOffset.UtcNow.AddDays(30)
    });
    return Ok(new { account });  // no tokens in body for web
}
else
{
    return Ok(new { accessToken, refreshToken, expiresIn = 3600, account });
}
```

---

## Refresh token rotation (all surfaces)

```
POST /v1/auth/refresh

1. Read token from: request body (mobile) OR hali_refresh cookie (web)
2. Compute SHA-256 hash of incoming raw token
3. Fetch refresh_tokens row by token_hash
4. If not found: 401
5. If replaced_by_token_hash is set (already rotated):
   → TOKEN THEFT DETECTED
   → Revoke ALL refresh_tokens for this account: UPDATE SET revoked_at = now()
   → Log security event with actor details
   → Return 401 code: "token_family_compromised"
6. If revoked_at set: 401
7. If expires_at < now(): 401
8. Generate new access token + new refresh token
9. Mark old row: revoked_at = now(), replaced_by_token_hash = new_hash
10. Insert new refresh_tokens row
11. Return / set new tokens (same surface detection as above)
```

---

## Authorization middleware

```csharp
// Role resolution — always from DB, never from JWT claims
public class HaliAuthorizationMiddleware
{
    public async Task InvokeAsync(HttpContext context, HaliDbContext db)
    {
        var accountId = context.User.GetAccountId();
        var account   = await db.Accounts.FindAsync(accountId);

        context.Items["Account"] = account;

        // Institution context (Phase 2+)
        if (account.AccountType == AccountType.InstitutionUser)
        {
            var membership = await db.InstitutionMemberships
                .Include(m => m.Institution)
                .FirstOrDefaultAsync(m => m.AccountId == accountId && m.IsActive);
            context.Items["InstitutionMembership"] = membership;
        }

        await next(context);
    }
}

// Authorization attributes
[RequireRole("institution_operator")]  // checks InstitutionMembership.Role
[RequireInstitutionScope]              // checks geo/category scope for the requested resource
```

---

## TOTP schema addition (Phase 2)

Add to `accounts` table via migration:

```sql
alter table accounts
    add column if not exists totp_secret       varchar(64),  -- TOTP secret (encrypted at rest)
    add column if not exists totp_enrolled_at  timestamptz;
```

---

## Security rules summary

- Refresh tokens stored as SHA-256 hex hashes — never plaintext
- Web session tokens in httpOnly, Secure, SameSite=Strict cookies — never localStorage
- TOTP secrets encrypted at rest (use AES-256 via Data Protection API)
- Magic link tokens expire in 15 minutes — single use
- OTP challenges expire in 10 minutes — max 3 attempts
- Token theft detection: re-use of rotated token revokes entire account token family
- All auth events written to admin_audit_logs
- 2FA is mandatory for all institution and ops users — no bypass path
