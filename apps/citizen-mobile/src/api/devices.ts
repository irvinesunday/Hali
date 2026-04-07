// apps/citizen-mobile/src/api/devices.ts
//
// Device push token registration.
// Result<T, ApiError> — no throws, no axios shim.

import { apiRequest } from './client';
import type { ApiError, RegisterPushTokenRequest, Result } from '../types/api';

/**
 * POST /v1/devices/push-token
 * Registers (or updates) the Expo push token for a device.
 * 422 device_not_found if the device hash isn't registered yet
 * (the device record is created during OTP verify, so this only fires
 * if push registration runs before auth — which it shouldn't).
 * Returns 204 on success.
 */
export async function registerPushToken(
  body: RegisterPushTokenRequest,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>('/v1/devices/push-token', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
  });
}
