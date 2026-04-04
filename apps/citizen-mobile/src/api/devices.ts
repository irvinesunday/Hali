import client from './client';
import type { RegisterPushTokenRequest } from '../types/api';

export async function registerPushToken(
  body: RegisterPushTokenRequest,
): Promise<void> {
  await client.post('/v1/devices/push-token', body);
}
