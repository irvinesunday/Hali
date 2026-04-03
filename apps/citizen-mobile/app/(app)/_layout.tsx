// Auth guard for the authenticated app stack.
// Redirects to /auth/phone if not authenticated.
import React, { useEffect } from 'react';
import { Stack, useRouter } from 'expo-router';
import { useAuthContext } from '../../src/context/AuthContext';

export default function AppLayout() {
  const router = useRouter();
  const { state } = useAuthContext();

  useEffect(() => {
    if (state.status === 'unauthenticated') {
      router.replace('/(auth)/phone');
    }
  }, [state.status, router]);

  if (state.status !== 'authenticated') {
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
