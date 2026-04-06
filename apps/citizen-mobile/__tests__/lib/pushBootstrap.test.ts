// apps/citizen-mobile/__tests__/lib/pushBootstrap.test.ts
//
// Tests the pure notificationDataToHref classifier. The hook itself
// (usePushBootstrap) and ensurePushRegistered have side effects we
// don't unit test here — they're exercised end-to-end during manual
// device testing.

// Mock expo-notifications, expo-crypto, expo-device, expo-application,
// and the AsyncStorage import so loading pushBootstrap.ts under Jest
// doesn't try to touch native modules. We only call the pure helper.
jest.mock('expo-notifications', () => ({
  getPermissionsAsync: jest.fn(),
  requestPermissionsAsync: jest.fn(),
  getExpoPushTokenAsync: jest.fn(),
  getLastNotificationResponseAsync: jest.fn(),
  addNotificationResponseReceivedListener: jest.fn(),
}));
jest.mock('expo-crypto', () => ({
  digestStringAsync: jest.fn(),
  CryptoDigestAlgorithm: { SHA256: 'SHA256' },
}));
jest.mock('expo-device', () => ({
  isDevice: false,
  modelName: 'test',
  osName: 'test',
  osVersion: '1.0',
  brand: 'test',
}));
jest.mock('expo-application', () => ({
  applicationId: 'com.test.hali',
}));
jest.mock('expo-router', () => ({
  router: { push: jest.fn() },
}));
jest.mock('../../src/api/devices', () => ({
  registerPushToken: jest.fn(),
}));

import { notificationDataToHref } from '../../src/lib/pushBootstrap';

describe('notificationDataToHref', () => {
  it('routes restoration_prompt to the modal', () => {
    expect(
      notificationDataToHref({
        notificationType: 'restoration_prompt',
        clusterId: 'abc-123',
      }),
    ).toBe('/(modals)/restoration/abc-123');
  });

  it('routes cluster_activated_in_followed_ward to cluster detail', () => {
    expect(
      notificationDataToHref({
        notificationType: 'cluster_activated_in_followed_ward',
        clusterId: 'xyz-789',
      }),
    ).toBe('/(app)/clusters/xyz-789');
  });

  it('routes cluster_resolved to cluster detail', () => {
    expect(
      notificationDataToHref({
        notificationType: 'cluster_resolved',
        clusterId: 'cluster-abc',
      }),
    ).toBe('/(app)/clusters/cluster-abc');
  });

  it('returns null for an unknown notificationType', () => {
    expect(
      notificationDataToHref({
        notificationType: 'totally_made_up',
        clusterId: 'abc',
      }),
    ).toBeNull();
  });

  it('returns null when clusterId is missing', () => {
    expect(
      notificationDataToHref({ notificationType: 'restoration_prompt' }),
    ).toBeNull();
  });

  it('returns null when clusterId is an empty string', () => {
    expect(
      notificationDataToHref({
        notificationType: 'restoration_prompt',
        clusterId: '',
      }),
    ).toBeNull();
  });

  it('returns null when notificationType is missing', () => {
    expect(notificationDataToHref({ clusterId: 'abc' })).toBeNull();
  });

  it('returns null for null input', () => {
    expect(notificationDataToHref(null)).toBeNull();
  });

  it('returns null for non-object input', () => {
    expect(notificationDataToHref('string')).toBeNull();
    expect(notificationDataToHref(42)).toBeNull();
    expect(notificationDataToHref(undefined)).toBeNull();
  });
});
