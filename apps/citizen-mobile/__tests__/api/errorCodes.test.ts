// apps/citizen-mobile/__tests__/api/errorCodes.test.ts
//
// Type-guard + parser-integration tests for the mobile ErrorCode
// discriminated union introduced by issue #154.
//
// Two contracts under test:
//
//   1. `isKnownErrorCode` recognises the wire-visible backend codes
//      mirrored in `ERROR_CODES`, and rejects everything else
//      (mobile-side sentinels, unknown future codes, non-strings).
//
//   2. `buildApiError` preserves the verbatim `code` string from the
//      canonical envelope, so unknown future backend codes still flow
//      end-to-end at runtime — the type's `(string & {})` escape hatch
//      is not a runtime lie.

import {
  ERROR_CODES,
  isKnownErrorCode,
  type ErrorCode,
} from '../../src/types/api';
import { buildApiError } from '../../src/api/client';

describe('isKnownErrorCode — known wire codes', () => {
  it.each(Object.values(ERROR_CODES))(
    'recognises %s as a known ErrorCode',
    (code) => {
      expect(isKnownErrorCode(code)).toBe(true);
    },
  );
});

describe('isKnownErrorCode — client-side sentinels (NOT known wire codes)', () => {
  it.each([
    'unknown_error',
    'network_error',
    'no_refresh_token',
    'session_expired',
  ])('rejects mobile sentinel %s', (sentinel) => {
    expect(isKnownErrorCode(sentinel)).toBe(false);
  });
});

describe('isKnownErrorCode — unknown / future backend codes', () => {
  it('rejects an unknown backend code (forward compatibility)', () => {
    // A future backend release might emit `quota.daily_exceeded` before the
    // mobile mirror catches up. The runtime must still see the verbatim
    // string, but the guard must report it as unknown so callers route
    // through their generic fallback path.
    expect(isKnownErrorCode('quota.daily_exceeded')).toBe(false);
  });

  it('rejects a code that almost matches but has a typo', () => {
    expect(isKnownErrorCode('rate_limit.exceededd')).toBe(false);
    expect(isKnownErrorCode('signal.duplicates')).toBe(false);
  });
});

describe('isKnownErrorCode — malformed / non-string values', () => {
  it.each<[string, unknown]>([
    ['undefined', undefined],
    ['null', null],
    ['number', 429],
    ['boolean true', true],
    ['boolean false', false],
    ['empty string', ''],
    ['object', { code: 'rate_limit.exceeded' }],
    ['array', ['rate_limit.exceeded']],
  ])('rejects %s', (_label, value) => {
    expect(isKnownErrorCode(value)).toBe(false);
  });
});

describe('buildApiError + isKnownErrorCode — parser integration', () => {
  it('preserves a known canonical code; guard recognises it', () => {
    const result = buildApiError(429, {
      error: {
        code: 'rate_limit.exceeded',
        message: 'Slow down.',
        traceId: '00-trace-1',
      },
    });

    expect(result.code).toBe('rate_limit.exceeded');
    expect(isKnownErrorCode(result.code)).toBe(true);

    // Narrowing flow that downstream call sites use.
    if (isKnownErrorCode(result.code)) {
      // After narrowing, comparison against a constant is valid.
      const isRateLimit: boolean =
        result.code === ERROR_CODES.RATE_LIMIT_EXCEEDED;
      expect(isRateLimit).toBe(true);
    }
  });

  it('preserves an unknown future backend code verbatim; guard rejects it', () => {
    const result = buildApiError(400, {
      error: {
        code: 'quota.daily_exceeded',
        message: 'Future code the app does not yet model.',
      },
    });

    // Runtime truth: the verbatim string survives the parser.
    expect(result.code).toBe('quota.daily_exceeded');
    // Type safety: the guard refuses to narrow.
    expect(isKnownErrorCode(result.code)).toBe(false);
  });

  it('regression (issue 152) — canonical envelope still carries message + traceId + details', () => {
    const result = buildApiError(400, {
      error: {
        code: 'validation.failed',
        message: 'Request is invalid.',
        details: { fields: { freeText: ['Required.'] } },
        traceId: '00-trace-regress',
      },
    });

    expect(result).toEqual({
      status: 400,
      code: 'validation.failed',
      message: 'Request is invalid.',
      details: { fields: { freeText: ['Required.'] } },
      traceId: '00-trace-regress',
    });
    expect(isKnownErrorCode(result.code)).toBe(true);
  });

  it('non-string code in envelope degrades to unknown_error sentinel; guard rejects', () => {
    const result = buildApiError(500, {
      error: { code: 42, message: 'Numeric code is malformed.' },
    });

    // Non-string `code` is treated as absent; parser falls back to its
    // own sentinel — which is intentionally NOT a known wire code.
    expect(result.code).toBe('unknown_error');
    expect(isKnownErrorCode(result.code)).toBe(false);
  });
});

describe('ErrorCode union — compile-time exhaustiveness aid', () => {
  it('every ERROR_CODES value is a member of the ErrorCode type', () => {
    // This runs at runtime but the assignment exists to lock in the
    // structural relationship: if a future edit accidentally widens
    // ERROR_CODES beyond the union, `tsc --noEmit` will fail here too.
    const allValues: ErrorCode[] = Object.values(ERROR_CODES);
    expect(allValues.length).toBeGreaterThan(0);
    expect(new Set(allValues).size).toBe(allValues.length);
  });
});
