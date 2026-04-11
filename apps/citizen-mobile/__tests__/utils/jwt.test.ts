// apps/citizen-mobile/__tests__/utils/jwt.test.ts
//
// Tests for the pure JWT decoder utility. No network, no React Native.

import {
  decodeJwtPayload,
  decodeAccountIdFromJwt,
} from '../../src/utils/jwt';

/**
 * Build a JWT with the given payload. The header and signature are fixed —
 * we never verify signatures on the client, so their values are irrelevant.
 */
function makeJwt(payload: Record<string, unknown>): string {
  const header = Buffer.from(
    JSON.stringify({ alg: 'HS256', typ: 'JWT' }),
  ).toString('base64url');
  const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const sig = 'fake-signature';
  return `${header}.${body}.${sig}`;
}

describe('decodeJwtPayload', () => {
  it('decodes a well-formed payload', () => {
    const token = makeJwt({ sub: 'abc-123', role: 'citizen', iat: 1700000000 });
    expect(decodeJwtPayload(token)).toEqual({
      sub: 'abc-123',
      role: 'citizen',
      iat: 1700000000,
    });
  });

  it('handles URL-safe base64 characters (- and _)', () => {
    // Payload that, when base64-encoded, contains URL-safe chars.
    const token = makeJwt({ sub: 'user?&+=', role: 'citizen' });
    const payload = decodeJwtPayload(token);
    expect(payload?.sub).toBe('user?&+=');
  });

  it('returns null for a token with the wrong number of segments', () => {
    expect(decodeJwtPayload('not.a.jwt.actually')).toBeNull();
    expect(decodeJwtPayload('onlyonepart')).toBeNull();
    expect(decodeJwtPayload('')).toBeNull();
  });

  it('returns null when the payload is not valid JSON', () => {
    const token = 'header.not-json.sig';
    expect(decodeJwtPayload(token)).toBeNull();
  });
});

describe('decodeAccountIdFromJwt', () => {
  it('extracts the sub claim as the account ID', () => {
    const token = makeJwt({ sub: '11111111-2222-3333-4444-555555555555' });
    expect(decodeAccountIdFromJwt(token)).toBe(
      '11111111-2222-3333-4444-555555555555',
    );
  });

  it('returns null when sub is missing', () => {
    const token = makeJwt({ role: 'citizen' });
    expect(decodeAccountIdFromJwt(token)).toBeNull();
  });

  it('returns null when sub is empty', () => {
    const token = makeJwt({ sub: '' });
    expect(decodeAccountIdFromJwt(token)).toBeNull();
  });

  it('returns null for a malformed token', () => {
    expect(decodeAccountIdFromJwt('malformed')).toBeNull();
  });
});
