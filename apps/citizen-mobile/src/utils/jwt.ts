// apps/citizen-mobile/src/utils/jwt.ts
//
// Pure JWT decoding utilities. No verification — we trust the token because
// it came from our own backend via HTTPS. Verification happens server-side.
//
// The backend's verify/refresh endpoints return only { accessToken,
// refreshToken, expiresIn }. The account ID is embedded in the access
// token's `sub` claim (see Hali.Application/Auth/AuthService.cs IssueAccessToken).
// This utility extracts it client-side so the mobile app can persist it to
// SecureStore without a separate API round-trip.

interface JwtPayload {
  sub?: string;
  role?: string;
  institution_id?: string;
  iat?: number;
  exp?: number;
}

/**
 * Decode a JWT payload without verifying the signature.
 * Returns null for malformed tokens. Does not throw.
 */
export function decodeJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    // Base64URL → Base64 (replace URL-safe chars, pad to multiple of 4)
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);

    // atob is available in React Native / Hermes — do not use Node's Buffer.
    const json = atob(padded);
    const parsed: unknown = JSON.parse(json);
    if (parsed === null || typeof parsed !== 'object') return null;
    return parsed as JwtPayload;
  } catch {
    return null;
  }
}

/**
 * Extract the account ID (JWT `sub` claim) from an access token.
 * Returns null if the token is malformed or the claim is missing.
 */
export function decodeAccountIdFromJwt(token: string): string | null {
  const payload = decodeJwtPayload(token);
  if (payload === null) return null;
  return typeof payload.sub === 'string' && payload.sub.length > 0
    ? payload.sub
    : null;
}
