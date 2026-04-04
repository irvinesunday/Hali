import client from './client';
import type {
  OtpRequestBody,
  VerifyOtpRequestBody,
  TokenResponse,
  RefreshRequestBody,
  LogoutRequestBody,
} from '../types/api';

export async function requestOtp(body: OtpRequestBody): Promise<void> {
  await client.post('/v1/auth/otp', body);
}

export async function verifyOtp(
  body: VerifyOtpRequestBody,
): Promise<TokenResponse> {
  const { data } = await client.post<TokenResponse>('/v1/auth/verify', body);
  return data;
}

export async function refreshToken(
  body: RefreshRequestBody,
): Promise<TokenResponse> {
  const { data } = await client.post<TokenResponse>('/v1/auth/refresh', body);
  return data;
}

export async function logout(body: LogoutRequestBody): Promise<void> {
  await client.post('/v1/auth/logout', body);
}
