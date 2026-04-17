// apps/citizen-mobile/__tests__/utils/otpErrorMessage.test.ts
//
// Pins the OTP-verify error → message mapping to the post-#158
// canonical wire codes. Issue #160 fixed the OTP screen branching
// on stale literals (`'invalid_otp'`, `'otp_expired'`) that no
// longer match anything the backend emits; these tests prove the
// branches now fire on `auth.otp_invalid` and that the dead
// `otp_expired` path is gone.

import { mapOtpVerifyErrorToMessage } from '../../src/utils/otpErrorMessage';
import { STRINGS } from '../../src/config/strings';
import { ERROR_CODES, type ApiError } from '../../src/types/api';

function makeError(overrides: Partial<ApiError> = {}): ApiError {
  return {
    status: 422,
    code: ERROR_CODES.AUTH_OTP_INVALID,
    message: 'Invalid or expired OTP.',
    ...overrides,
  };
}

describe('mapOtpVerifyErrorToMessage', () => {
  it('returns OTP_INVALID for the canonical auth.otp_invalid wire code', () => {
    expect(
      mapOtpVerifyErrorToMessage(
        makeError({ code: ERROR_CODES.AUTH_OTP_INVALID }),
      ),
    ).toBe(STRINGS.AUTH.OTP_INVALID);
  });

  it('does NOT branch on HTTP 401 alone (verify endpoint never emits 401)', () => {
    // The backend's verify path throws `ValidationException`, which
    // `ExceptionToApiErrorMapper` maps to HTTP 400 — never 401. A
    // bare 401 with an unrelated code must fall through to the
    // generic message rather than being misclassified as bad-OTP.
    expect(
      mapOtpVerifyErrorToMessage(
        makeError({
          status: 401,
          code: 'unknown_error',
          message: 'unrelated',
        }),
      ),
    ).toBe('unrelated');
  });

  it('does NOT branch on the stale pre-PR158 literal "invalid_otp"', () => {
    // The old screen branched on `'invalid_otp'` directly. Backend
    // now emits `auth.otp_invalid`. A leaked stale literal must not
    // be treated as a known invalid-OTP signal — it must fall
    // through to the generic fallback path.
    const result = mapOtpVerifyErrorToMessage(
      makeError({ status: 422, code: 'invalid_otp', message: '' }),
    );
    expect(result).toBe(STRINGS.AUTH.OTP_VERIFY_FAILED);
    expect(result).not.toBe(STRINGS.AUTH.OTP_INVALID);
  });

  it('does NOT branch on the dead pre-PR158 literal "otp_expired"', () => {
    // No `otp_expired` code exists in the canonical catalog;
    // expired OTPs are folded into `auth.otp_invalid` server-side.
    const result = mapOtpVerifyErrorToMessage(
      makeError({ status: 422, code: 'otp_expired', message: '' }),
    );
    expect(result).toBe(STRINGS.AUTH.OTP_VERIFY_FAILED);
  });

  it('falls back to the server message for an unknown error code', () => {
    expect(
      mapOtpVerifyErrorToMessage(
        makeError({
          status: 500,
          code: 'server.internal_error',
          message: 'Server is having a moment.',
        }),
      ),
    ).toBe('Server is having a moment.');
  });

  it('falls back to OTP_VERIFY_FAILED when the unknown-code error has no message', () => {
    expect(
      mapOtpVerifyErrorToMessage(
        makeError({
          status: 500,
          code: 'server.internal_error',
          message: '',
        }),
      ),
    ).toBe(STRINGS.AUTH.OTP_VERIFY_FAILED);
  });
});
