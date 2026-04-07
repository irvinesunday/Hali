// apps/citizen-mobile/app/(app)/_layout.tsx
//
// Authenticated app layout.
//
// Two responsibilities beyond rendering the stack:
//   1. Auth guard — redirect unauthenticated users to /(auth)/phone
//   2. Mount the runtime bootstraps that should only run when authed:
//      - usePushBootstrap()  — token registration + deep link routing
//      - initOfflineQueue()  — load persisted queue + listen for flush triggers
//
// These are intentionally inside the (app) layout (not the root layout)
// because both depend on having a valid session — push registration
// hits an [Authorize] endpoint, and the offline queue's submit fn calls
// authenticated services.

import React, { useEffect } from 'react';
import { Stack, useRouter } from 'expo-router';
import { useAuth } from '../../src/context/AuthContext';
import { usePushBootstrap } from '../../src/lib/pushBootstrap';
import { initOfflineQueue } from '../../src/lib/offlineQueueBootstrap';

export default function AppLayout(): React.ReactElement | null {
  const router = useRouter();
  const { authState } = useAuth();

  // Auth guard
  useEffect(() => {
    if (authState.status === 'unauthenticated') {
      router.replace('/(auth)/phone');
    }
  }, [authState.status, router]);

  // Push registration + deep link routing — runs once per mount
  usePushBootstrap();

  // Offline queue runtime — load persisted items + start listeners
  useEffect(() => {
    void initOfflineQueue();
  }, []);

  if (authState.status !== 'authenticated') {
    return null;
  }

  return (
    <Stack
      screenOptions={{
        headerShown: false,
        animation: 'slide_from_right',
      }}
    />
  );
}
