// apps/citizen-mobile/__tests__/utils/wardsUpdateErrorMessage.test.ts
//
// Pins the followed-localities update error → toast mapping to the
// post-#158 canonical wire codes. Issue #160 fixed the wards screen
// branching on the stale literal `'max_followed_localities_exceeded'`
// that no longer matches anything the backend emits; this test
// proves the over-capacity toast now fires on the canonical
// `validation.max_followed_localities_exceeded` code.

import {
  formatCapacityToastMessage,
  mapWardsUpdateErrorToToast,
} from '../../src/utils/wardsUpdateErrorMessage';
import { ERROR_CODES, type ApiError } from '../../src/types/api';

const MAX = 5;

function makeError(overrides: Partial<ApiError> = {}): ApiError {
  return {
    status: 422,
    code: ERROR_CODES.VALIDATION_MAX_FOLLOWED_LOCALITIES_EXCEEDED,
    message: 'Maximum followed localities exceeded.',
    ...overrides,
  };
}

describe('formatCapacityToastMessage', () => {
  // Pinned so the client-side at-capacity guard in wards.tsx and
  // the server-side error branch in mapWardsUpdateErrorToToast
  // cannot drift on copy tweaks — both call sites read from this
  // single source of truth.
  it('returns the canonical over-capacity toast for the configured max', () => {
    expect(formatCapacityToastMessage(MAX)).toBe(
      `You can follow up to ${MAX} areas.`,
    );
  });
});

describe('mapWardsUpdateErrorToToast', () => {
  it('returns the over-capacity toast for the canonical wire code', () => {
    expect(
      mapWardsUpdateErrorToToast(
        makeError({
          code: ERROR_CODES.VALIDATION_MAX_FOLLOWED_LOCALITIES_EXCEEDED,
        }),
        { maxFollowedWards: MAX },
      ),
    ).toBe(formatCapacityToastMessage(MAX));
  });

  it('does NOT branch on the stale pre-PR158 literal', () => {
    // The old screen branched on `'max_followed_localities_exceeded'`
    // directly. Backend now emits
    // `validation.max_followed_localities_exceeded`. The bare stale
    // literal must NOT trigger the over-capacity toast; it must fall
    // through to the server-supplied message.
    const result = mapWardsUpdateErrorToToast(
      makeError({
        code: 'max_followed_localities_exceeded',
        message: 'server-supplied fallback',
      }),
      { maxFollowedWards: MAX },
    );
    expect(result).toBe('server-supplied fallback');
    expect(result).not.toBe(formatCapacityToastMessage(MAX));
  });

  it('falls back to the server message for an unrelated known error code', () => {
    expect(
      mapWardsUpdateErrorToToast(
        makeError({
          code: ERROR_CODES.VALIDATION_FAILED,
          message: 'Validation failed.',
        }),
        { maxFollowedWards: MAX },
      ),
    ).toBe('Validation failed.');
  });

  it('falls back to the server message for an unknown future code', () => {
    // Forward compatibility — unknown codes flow through verbatim
    // and the toast surfaces the server message rather than guessing.
    expect(
      mapWardsUpdateErrorToToast(
        makeError({
          code: 'quota.daily_exceeded',
          message: 'Daily quota exceeded.',
        }),
        { maxFollowedWards: MAX },
      ),
    ).toBe('Daily quota exceeded.');
  });
});
