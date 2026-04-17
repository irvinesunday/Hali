// apps/citizen-mobile/src/api/auth.ts
//
// Auth service layer — all API calls for authentication.
// Returns Result<T, ApiError> — never throws.
// Screens import from here; never call fetch() inline.

import { apiRequest } from './client';
import type {
  ApiError,
  Result,
  OtpRequestBody,
  RequestOtpResponse,
  VerifyOtpRequestBody,
  VerifyOtpResponse,
  RefreshRequestBody,
  RefreshTokenResponse,
  LogoutRequestBody,
} from '../types/api';

/**
 * POST /v1/auth/otp
 * Requests an OTP to be sent to the destination phone number.
 * No auth header (user is not yet signed in).
 */
export async function requestOtp(
  body: OtpRequestBody,
): Promise<Result<RequestOtpResponse, ApiError>> {
  return apiRequest<RequestOtpResponse>('/v1/auth/otp', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
    withAuth: false,
  });
}

/**
 * POST /v1/auth/verify
 * Exchanges an OTP for a token pair + accountId.
 * No auth header.
 */
export async function verifyOtp(
  body: VerifyOtpRequestBody,
): Promise<Result<VerifyOtpResponse, ApiError>> {
  return apiRequest<VerifyOtpResponse>('/v1/auth/verify', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
    withAuth: false,
  });
}

/**
 * POST /v1/auth/refresh
 * Rotates the refresh token. Used by the API client's 401 interceptor.
 * No auth header — the refresh token in the body is the credential.
 */
export async function refreshToken(
  body: RefreshRequestBody,
): Promise<Result<RefreshTokenResponse, ApiError>> {
  return apiRequest<RefreshTokenResponse>('/v1/auth/refresh', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
    withAuth: false,
  });
}

/**
 * POST /v1/auth/logout
 * Best-effort revocation of the refresh token server-side.
 * No auth header — logout must work even when the access token has expired.
 * Callers should swallow errors and clear local session regardless.
 */
export async function logout(
  body: LogoutRequestBody,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>('/v1/auth/logout', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
    withAuth: false,
  });
}
