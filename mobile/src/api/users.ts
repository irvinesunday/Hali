/**
 * Users API stubs — aligned to patched OpenAPI spec.
 *
 * GET /v1/users/me
 * PUT /v1/users/me/notification-settings
 */

import { apiFetch } from './client';

export type UserProfile = {
  id: string;
  displayName: string | null;
  phone: string | null;
  accountType: string;
  createdAt: string;
};

export type NotificationSettings = {
  restorationPrompts?: boolean;
  clusterActivations?: boolean;
  officialUpdates?: boolean;
};

export function getMyProfile(): Promise<UserProfile> {
  return apiFetch('/v1/users/me', { method: 'GET' });
}

export function updateNotificationSettings(
  settings: NotificationSettings,
  idempotencyKey: string,
): Promise<void> {
  return apiFetch('/v1/users/me/notification-settings', {
    method: 'PUT',
    body: JSON.stringify(settings),
    idempotencyKey,
  });
}
