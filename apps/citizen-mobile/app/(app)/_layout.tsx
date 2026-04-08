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

  // Auth guard — guest mode is allowed for browsing.
  // Contribution actions (participate, submit) gate on auth inline.
  // Only redirect if status is 'unauthenticated' AND the user explicitly
  // tried to reach a protected route (handled per-action, not here).
  // Loading state renders null while tokens are being checked.

  // Push registration + deep link routing — runs once per mount
  usePushBootstrap();

  // Offline queue runtime — load persisted items + start listeners
  useEffect(() => {
    void initOfflineQueue();
  }, []);

  // Allow both authenticated and unauthenticated (guest) users through.
  // 'unknown' state still renders null to prevent flash of wrong screen.
  if (authState.status === 'unknown') {
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
