import client from './client';
import type { UserMeResponse, NotificationSettings } from '../types/api';

export async function getMe(): Promise<UserMeResponse> {
  const { data } = await client.get<UserMeResponse>('/v1/users/me');
  return data;
}

export async function updateNotificationSettings(
  settings: NotificationSettings,
): Promise<void> {
  await client.put('/v1/users/me/notification-settings', settings);
}
