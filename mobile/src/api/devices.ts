/**
 * Devices API stubs — aligned to patched OpenAPI spec.
 *
 * POST /v1/devices/push-token
 */

import { apiFetch } from './client';

export type RegisterPushTokenRequest = {
  expoPushToken: string;
  deviceFingerprint: string;
};

export function registerPushToken(
  body: RegisterPushTokenRequest,
  idempotencyKey: string,
): Promise<void> {
  return apiFetch('/v1/devices/push-token', {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}
