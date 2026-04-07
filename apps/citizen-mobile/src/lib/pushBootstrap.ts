// apps/citizen-mobile/src/lib/pushBootstrap.ts
//
// Push notification bootstrap.
//
// Two responsibilities:
//   1. Register the device's Expo push token with the backend after
//      successful auth (idempotent — re-registers silently if the
//      token has rotated).
//   2. Wire up a notification response listener that maps incoming
//      deep links to Expo Router routes.
//
// Notification deep link payloads (data field):
//   { notificationType: 'restoration_prompt', clusterId: '...' }
//   { notificationType: 'cluster_activated_in_followed_ward', clusterId: '...' }
//   { notificationType: 'cluster_resolved', clusterId: '...' }
//
// All routes are scoped under (app)/ so the listener only fires when
// the user is authenticated (the (app) layout owns this hook).

import { useEffect, useRef } from 'react';
import * as Notifications from 'expo-notifications';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { router, type Href } from 'expo-router';
import { registerPushToken } from '../api/devices';

const STORED_TOKEN_KEY = 'hali_expo_push_token_v1';
// Stored in AsyncStorage so we only re-register when the token actually
// changes between launches.

async function getDeviceHash(): Promise<string> {
  const parts = [
    Device.modelName ?? 'unknown',
    Device.osName ?? 'unknown',
    Device.osVersion ?? 'unknown',
    Device.brand ?? 'unknown',
    Application.applicationId ?? 'unknown',
  ].join('|');
  return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, parts);
}

/**
 * Register (or re-register) the Expo push token with the backend.
 *
 * Returns silently on permission denial or any error — push registration
 * is non-blocking and the user shouldn't see a failure here. They can
 * always re-trigger it from the notifications settings screen.
 */
export async function ensurePushRegistered(): Promise<void> {
  try {
    if (!Device.isDevice) {
      // Push notifications don't work on simulators / web
      return;
    }

    // Permission check (request if not yet asked)
    const { status: existingStatus } =
      await Notifications.getPermissionsAsync();
    let finalStatus = existingStatus;
    if (existingStatus !== 'granted') {
      const { status: requestedStatus } =
        await Notifications.requestPermissionsAsync();
      finalStatus = requestedStatus;
    }
    if (finalStatus !== 'granted') return;

    const tokenResult = await Notifications.getExpoPushTokenAsync();
    const token = tokenResult.data;

    // Compare against the locally stored token to avoid spamming the
    // backend on every app launch when nothing has changed.
    const AsyncStorage = (
      await import('@react-native-async-storage/async-storage')
    ).default;
    const stored = await AsyncStorage.getItem(STORED_TOKEN_KEY);
    if (stored === token) return;

    const deviceHash = await getDeviceHash();
    const result = await registerPushToken({
      expoPushToken: token,
      deviceHash,
    });

    if (result.ok) {
      await AsyncStorage.setItem(STORED_TOKEN_KEY, token);
    }
    // On failure, leave stored as-is so the next launch retries.
  } catch {
    // Push bootstrap is non-essential — never throw to the layout.
  }
}

/**
 * Map a notification's data payload to an Expo Router href.
 * Pure function — exported for testing.
 *
 * Returns null if the payload doesn't match a known notification type
 * or is missing the required clusterId.
 */
export function notificationDataToHref(data: unknown): Href | null {
  if (data === null || typeof data !== 'object') return null;
  const d = data as Record<string, unknown>;
  if (typeof d.clusterId !== 'string' || d.clusterId.length === 0) return null;
  if (typeof d.notificationType !== 'string') return null;

  switch (d.notificationType) {
    case 'restoration_prompt':
      return `/(modals)/restoration/${d.clusterId}`;
    case 'cluster_activated_in_followed_ward':
    case 'cluster_resolved':
      return `/(app)/clusters/${d.clusterId}`;
    default:
      return null;
  }
}

/**
 * React hook that mounts both the auto push registration and the
 * notification response listener. Call this once from inside the
 * authenticated layout.
 *
 * Cold-launch deep linking: when the app is launched by tapping a
 * notification, getLastNotificationResponseAsync resolves the payload
 * once on first mount.
 */
export function usePushBootstrap(): void {
  const handledColdLaunch = useRef(false);

  // 1. Auto-register on mount (first authenticated screen view)
  useEffect(() => {
    void ensurePushRegistered();
  }, []);

  // 2. Cold launch — handle a notification that launched the app
  useEffect(() => {
    if (handledColdLaunch.current) return;
    handledColdLaunch.current = true;

    void (async () => {
      const last = await Notifications.getLastNotificationResponseAsync();
      if (last !== null) {
        const href = notificationDataToHref(
          last.notification.request.content.data,
        );
        if (href !== null) router.push(href);
      }
    })();
  }, []);

  // 3. Foreground tap — handle a notification tapped while running
  useEffect(() => {
    const sub = Notifications.addNotificationResponseReceivedListener(
      (response) => {
        const href = notificationDataToHref(
          response.notification.request.content.data,
        );
        if (href !== null) router.push(href);
      },
    );
    return () => sub.remove();
  }, []);
}
