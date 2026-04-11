// Splash / Boot screen
// Restores auth state from SecureStore then routes accordingly.
import React, { useEffect } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useAuthContext } from '../src/context/AuthContext';
import { Colors } from '../src/theme/colors';

export default function SplashScreen() {
  const router = useRouter();
  const { state } = useAuthContext();

  useEffect(() => {
    if (state.status === 'authenticated') {
      router.replace('/(app)/home');
    } else if (state.status === 'unauthenticated') {
      router.replace('/(auth)/phone');
    }
    // While status === 'unknown' (bootstrapping) we stay on this screen
  }, [state.status, router]);

  return (
    <View style={styles.container}>
      <Text style={styles.wordmark}>hali</Text>
      <Text style={styles.tagline}>Civic pulse, clearly.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  wordmark: {
    fontSize: 52,
    fontWeight: '800',
    color: Colors.primaryForeground,
    letterSpacing: -1,
  },
  tagline: { fontSize: 16, color: Colors.primarySubtle },
});
