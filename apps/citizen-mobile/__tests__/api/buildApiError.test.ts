// apps/citizen-mobile/__tests__/api/buildApiError.test.ts
//
// Unit tests for the canonical-envelope error parser used by every
// request path in `src/api/client.ts`. The parser must:
//
//   1. Read `error.code`, `error.message`, `error.details`, `error.traceId`
//      from the canonical `{ error: { ... } }` envelope emitted by the
//      Hali backend after H1 + H2.
//   2. Fall back to the three pre-H1 shapes when a legacy or drifted
//      endpoint does not emit the canonical envelope.
//   3. Never throw; degrade malformed / missing / non-JSON bodies to
//      `code: 'unknown_error'` with a generic message.

import { buildApiError } from '../../src/api/client';

describe('buildApiError — canonical envelope', () => {
  it('extracts code and message from a canonical envelope', () => {
    const body = {
      error: {
        code: 'validation.invalid_category',
        message: 'Category is not a known taxonomy slug.',
        traceId: '00-abc-123-00',
      },
    };

    const result = buildApiError(400, body);

    expect(result).toEqual({
      status: 400,
      code: 'validation.invalid_category',
      message: 'Category is not a known taxonomy slug.',
      traceId: '00-abc-123-00',
    });
  });

  it('preserves `details` when present (object payload)', () => {
    const body = {
      error: {
        code: 'validation.failed',
        message: 'Request is invalid.',
        details: {
          fields: {
            freeText: ['Required.'],
            latitude: ['Out of range.'],
          },
        },
        traceId: 'trace-xyz',
      },
    };

    const result = buildApiError(400, body);

    expect(result.code).toBe('validation.failed');
    expect(result.traceId).toBe('trace-xyz');
    expect(result.details).toEqual({
      fields: {
        freeText: ['Required.'],
        latitude: ['Out of range.'],
      },
    });
  });

  it('preserves `details` when the backend sends an array', () => {
    const body = {
      error: {
        code: 'validation.failed',
        message: 'Multiple issues.',
        details: ['issue-a', 'issue-b'],
        traceId: 't',
      },
    };

    const result = buildApiError(400, body);

    expect(result.details).toEqual(['issue-a', 'issue-b']);
  });

  it('preserves `traceId` end-to-end', () => {
    const body = {
      error: {
        code: 'auth.unauthenticated',
        message: 'Authentication required.',
        traceId: '00-f00d-b47e-01',
      },
    };

    const result = buildApiError(401, body);

    expect(result.traceId).toBe('00-f00d-b47e-01');
  });

  it('omits `traceId` when the field is missing', () => {
    const body = {
      error: {
        code: 'signal.duplicate',
        message: 'Signal already submitted.',
      },
    };

    const result = buildApiError(409, body);

    expect(result.code).toBe('signal.duplicate');
    expect(result.message).toBe('Signal already submitted.');
    expect('traceId' in result).toBe(false);
    expect('details' in result).toBe(false);
  });

  it('omits `details` when the backend sends null', () => {
    const body = {
      error: {
        code: 'cluster.not_found',
        message: 'Cluster not found.',
        details: null,
        traceId: 'abc',
      },
    };

    const result = buildApiError(404, body);

    expect('details' in result).toBe(false);
    expect(result.traceId).toBe('abc');
  });

  it('omits `traceId` when the backend sends an empty string', () => {
    const body = {
      error: {
        code: 'auth.forbidden',
        message: 'Access denied.',
        traceId: '',
      },
    };

    const result = buildApiError(403, body);

    expect('traceId' in result).toBe(false);
  });

  it('degrades to unknown_error when canonical envelope is missing code', () => {
    const body = {
      error: {
        message: 'Something went wrong.',
        traceId: 't',
      },
    };

    const result = buildApiError(500, body);

    expect(result.code).toBe('unknown_error');
    expect(result.message).toBe('Something went wrong.');
    expect(result.traceId).toBe('t');
  });

  it('degrades message when canonical envelope is missing message', () => {
    const body = {
      error: {
        code: 'dependency.nlp_unavailable',
      },
    };

    const result = buildApiError(503, body);

    expect(result.code).toBe('dependency.nlp_unavailable');
    expect(result.message).toBe('An unexpected error occurred.');
  });

  it('ignores non-string code/message/traceId values', () => {
    const body = {
      error: {
        code: 42,
        message: { nested: 'not a string' },
        traceId: 99,
        details: { fields: { a: ['b'] } },
      },
    };

    const result = buildApiError(400, body);

    expect(result.code).toBe('unknown_error');
    expect(result.message).toBe('An unexpected error occurred.');
    expect('traceId' in result).toBe(false);
    // details is passed through untyped
    expect(result.details).toEqual({ fields: { a: ['b'] } });
  });
});

describe('buildApiError — legacy fallbacks', () => {
  it('parses legacy { error: "..." } string-only shape', () => {
    const body = { error: 'OTP code has expired.' };

    const result = buildApiError(400, body);

    expect(result).toEqual({
      status: 400,
      code: 'unknown_error',
      message: 'OTP code has expired.',
    });
  });

  it('parses legacy { error, code } shape', () => {
    const body = { error: 'Invalid taxonomy slug.', code: 'validation.failed' };

    const result = buildApiError(422, body);

    expect(result).toEqual({
      status: 422,
      code: 'validation.failed',
      message: 'Invalid taxonomy slug.',
    });
  });

  it('parses legacy { code, message } shape', () => {
    const body = {
      code: 'integrity.rate_limited',
      message: 'Too many requests.',
    };

    const result = buildApiError(429, body);

    expect(result).toEqual({
      status: 429,
      code: 'integrity.rate_limited',
      message: 'Too many requests.',
    });
  });

  it('treats an array-valued `error` as non-canonical and falls back', () => {
    // `typeof [] === 'object'`; guard against arrays masquerading as the
    // canonical envelope.
    const body = { error: ['a', 'b'], code: 'legacy.code', message: 'm' };

    const result = buildApiError(400, body);

    expect(result.code).toBe('legacy.code');
    expect(result.message).toBe('m');
  });

  it('treats null `error` as non-canonical and falls back', () => {
    const body = { error: null, code: 'legacy.only_code' };

    const result = buildApiError(400, body);

    expect(result.code).toBe('legacy.only_code');
  });
});

describe('buildApiError — resilience / malformed bodies', () => {
  it('returns unknown_error for a null body (non-JSON or empty response)', () => {
    const result = buildApiError(500, null);

    expect(result).toEqual({
      status: 500,
      code: 'unknown_error',
      message: 'An unexpected error occurred.',
    });
  });

  it('returns unknown_error for an undefined body', () => {
    const result = buildApiError(500, undefined);

    expect(result.code).toBe('unknown_error');
    expect(result.message).toBe('An unexpected error occurred.');
    expect(result.status).toBe(500);
  });

  it('returns unknown_error for a string body', () => {
    const result = buildApiError(502, 'Bad Gateway');

    expect(result.code).toBe('unknown_error');
  });

  it('returns unknown_error for an empty object body', () => {
    const result = buildApiError(400, {});

    expect(result).toEqual({
      status: 400,
      code: 'unknown_error',
      message: 'An unexpected error occurred.',
    });
  });

  it('returns unknown_error for a body whose `error` is a boolean', () => {
    const result = buildApiError(400, { error: true });

    expect(result.code).toBe('unknown_error');
  });

  it('always preserves the HTTP status verbatim', () => {
    const statuses = [0, 400, 401, 403, 404, 409, 422, 429, 500, 502, 503];
    for (const status of statuses) {
      expect(buildApiError(status, null).status).toBe(status);
    }
  });
});
