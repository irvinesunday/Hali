// apps/citizen-mobile/app/(modals)/_layout.tsx
//
// Modal route group. All routes under (modals)/ are presented as
// modals over the current screen stack via Expo Router.

import React from 'react';
import { Stack } from 'expo-router';

export default function ModalsLayout(): React.ReactElement {
  return (
    <Stack
      screenOptions={{
        presentation: 'modal',
        headerShown: false,
      }}
    />
  );
}
