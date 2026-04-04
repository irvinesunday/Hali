import React, { useEffect, useRef } from 'react';
import { Animated, Text, StyleSheet } from 'react-native';

interface ToastProps {
  message: string;
  visible: boolean;
  variant?: 'info' | 'error' | 'success';
}

export function Toast({ message, visible, variant = 'info' }: ToastProps) {
  const opacity = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (visible) {
      Animated.sequence([
        Animated.timing(opacity, {
          toValue: 1,
          duration: 200,
          useNativeDriver: true,
        }),
        Animated.delay(2400),
        Animated.timing(opacity, {
          toValue: 0,
          duration: 300,
          useNativeDriver: true,
        }),
      ]).start();
    }
  }, [visible, message, opacity]);

  return (
    <Animated.View
      style={[
        styles.container,
        variant === 'error' && styles.error,
        variant === 'success' && styles.success,
        { opacity },
      ]}
      pointerEvents="none"
    >
      <Text style={styles.text}>{message}</Text>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    bottom: 90,
    left: 24,
    right: 24,
    backgroundColor: '#1f2937',
    borderRadius: 10,
    paddingVertical: 12,
    paddingHorizontal: 16,
    alignItems: 'center',
    zIndex: 999,
  },
  error: { backgroundColor: '#991b1b' },
  success: { backgroundColor: '#166534' },
  text: { color: '#fff', fontSize: 14, fontWeight: '500' },
});
