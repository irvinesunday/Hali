/**
 * Auth API stubs — aligned to patched OpenAPI spec.
 *
 * POST /v1/auth/otp          (was /request-otp)
 * POST /v1/auth/verify       (was /verify-otp)
 * POST /v1/auth/refresh
 * POST /v1/auth/logout
 */

import { apiFetch } from './client';

export type RequestOtpRequest = {
  method: 'phone_otp';
  destination: string;
};

export type RequestOtpResponse = {
  challengeId: string;
};

export type VerifyOtpRequest = {
  challengeId: string;
  otpCode: string;
  deviceFingerprint: string;
};

export type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
};

export type RefreshTokenRequest = {
  refreshToken: string;
};

export function requestOtp(
  body: RequestOtpRequest,
  idempotencyKey: string,
): Promise<RequestOtpResponse> {
  return apiFetch('/v1/auth/otp', {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}

export function verifyOtp(
  body: VerifyOtpRequest,
  idempotencyKey: string,
): Promise<TokenResponse> {
  return apiFetch('/v1/auth/verify', {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}

export function refreshToken(body: RefreshTokenRequest): Promise<TokenResponse> {
  return apiFetch('/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function logout(refreshToken?: string): Promise<void> {
  return apiFetch('/v1/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  });
}
