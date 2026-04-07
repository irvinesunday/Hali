// apps/citizen-mobile/src/api/users.ts
//
// Account profile + notification settings.
// Result<T, ApiError> — no throws, no axios shim.

import { apiRequest } from './client';
import type {
  ApiError,
  NotificationSettings,
  Result,
  UserMeResponse,
} from '../types/api';

/**
 * GET /v1/users/me
 * Returns the current account's profile + notification settings.
 * 401 if unauthenticated, 404 if account not found.
 */
export async function getMe(): Promise<Result<UserMeResponse, ApiError>> {
  return apiRequest<UserMeResponse>('/v1/users/me', { method: 'GET' });
}

/**
 * PUT /v1/users/me/notification-settings
 * Updates the three boolean preference flags.
 * Returns 204 on success.
 */
export async function updateNotificationSettings(
  body: NotificationSettings,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>('/v1/users/me/notification-settings', {
    method: 'PUT',
    body: body as unknown as Record<string, unknown>,
  });
}
