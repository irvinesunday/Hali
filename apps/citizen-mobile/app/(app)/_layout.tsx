// apps/citizen-mobile/app/(app)/_layout.tsx
//
// App layout.
//
// Two responsibilities beyond rendering the stack:
//   1. Auth gating — guest mode is allowed for browsing; only render
//      null while auth status is 'unknown' to avoid a flash of the
//      wrong screen.
//   2. Mount the runtime bootstraps that should only run when authed:
//      - usePushBootstrap()  — token registration + deep link routing
//      - initOfflineQueue()  — load persisted queue + listen for flush triggers
//
// Both bootstraps depend on having a valid session — push registration
// hits an [Authorize] endpoint, and the offline queue's flush will drop
// queued writes on 401. They are gated on authState.status === 'authenticated'
// so guest users do not trigger authenticated network calls.

import React, { useEffect } from 'react';
import { Stack } from 'expo-router';
import { useAuth } from '../../src/context/AuthContext';
import { usePushBootstrap } from '../../src/lib/pushBootstrap';
import { initOfflineQueue } from '../../src/lib/offlineQueueBootstrap';

export default function AppLayout(): React.ReactElement | null {
  const { authState } = useAuth();
  const isAuthenticated = authState.status === 'authenticated';

  // Push registration + deep link routing — only when authenticated.
  usePushBootstrap(isAuthenticated);

  // Offline queue runtime — only when authenticated.
  useEffect(() => {
    if (!isAuthenticated) return;
    void initOfflineQueue();
  }, [isAuthenticated]);

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
