// apps/citizen-mobile/src/utils/otpErrorMessage.ts
//
// Pure mapping from a verify-OTP `ApiError` to the user-facing message
// the OTP screen renders. Extracted so the branch logic can be unit
// tested without React Native (mirrors the `composerGates` pattern).
//
// Backend contract (post-#158):
//   - `auth.otp_invalid` is emitted for both wrong AND expired OTPs;
//     `AuthService.AuthenticateAsync` folds them together with the
//     message "Invalid or expired OTP." There is no distinct
//     `otp_expired` wire code in the canonical catalog
//     (`02_openapi.yaml#/components/schemas/ErrorCode`).
//   - HTTP 401 from the verify endpoint is treated as the same
//     class of failure (defence in depth — the framework auth
//     pipeline never serves the verify route, but the belt-and-braces
//     check matches the prior screen behavior).

import { STRINGS } from '../config/strings';
import {
  ERROR_CODES,
  isKnownErrorCode,
  type ApiError,
} from '../types/api';

export function mapOtpVerifyErrorToMessage(error: ApiError): string {
  const isInvalidOtpCode =
    isKnownErrorCode(error.code) && error.code === ERROR_CODES.AUTH_OTP_INVALID;

  if (error.status === 401 || isInvalidOtpCode) {
    return STRINGS.AUTH.OTP_INVALID;
  }

  return error.message || STRINGS.AUTH.OTP_VERIFY_FAILED;
}
