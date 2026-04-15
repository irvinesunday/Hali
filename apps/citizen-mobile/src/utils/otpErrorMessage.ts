// apps/citizen-mobile/src/utils/otpErrorMessage.ts
//
// Pure mapping from a verify-OTP `ApiError` to the user-facing message
// the OTP screen renders. Extracted so the branch logic can be unit
// tested without React Native (mirrors the `composerGates` pattern).
//
// Backend contract (post-PR158):
//   - `auth.otp_invalid` is emitted for both wrong AND expired OTPs;
//     `AuthService.AuthenticateAsync` throws `ValidationException`
//     with the message "Invalid or expired OTP." which the server
//     maps to HTTP 400 via `ExceptionToApiErrorMapper`. There is no
//     distinct `otp_expired` wire code in the canonical catalog
//     (`02_openapi.yaml#/components/schemas/ErrorCode`) and HTTP 401
//     is never emitted for the verify endpoint, so the canonical
//     code is the single source of truth for this branch.

import { STRINGS } from '../config/strings';
import {
  ERROR_CODES,
  isKnownErrorCode,
  type ApiError,
} from '../types/api';

export function mapOtpVerifyErrorToMessage(error: ApiError): string {
  if (
    isKnownErrorCode(error.code) &&
    error.code === ERROR_CODES.AUTH_OTP_INVALID
  ) {
    return STRINGS.AUTH.OTP_INVALID;
  }

  return error.message || STRINGS.AUTH.OTP_VERIFY_FAILED;
}
