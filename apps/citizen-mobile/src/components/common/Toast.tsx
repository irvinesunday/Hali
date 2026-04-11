import React, { useEffect } from 'react';
import { Text, StyleSheet } from 'react-native';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  withTiming,
  withSequence,
  withDelay,
  Easing,
} from 'react-native-reanimated';
import { Colors, FontFamily, FontSize, Spacing, Radius } from '../../theme';

interface ToastProps {
  message: string;
  visible: boolean;
  variant?: 'info' | 'error' | 'success';
}

export function Toast({ message, visible, variant = 'info' }: ToastProps) {
  const opacity = useSharedValue(0);

  useEffect(() => {
    if (visible) {
      opacity.value = withSequence(
        withTiming(1, { duration: 200, easing: Easing.out(Easing.ease) }),
        withDelay(
          2400,
          withTiming(0, { duration: 300, easing: Easing.in(Easing.ease) }),
        ),
      );
    }
  }, [visible, message]);

  const animatedStyle = useAnimatedStyle(() => ({ opacity: opacity.value }));

  return (
    <Animated.View
      style={[
        styles.container,
        variant === 'error' && styles.error,
        variant === 'success' && styles.success,
        animatedStyle,
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
    bottom: 96,
    left: Spacing.lg,
    right: Spacing.lg,
    backgroundColor: Colors.foreground,
    borderRadius: Radius.lg,
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.lg,
    alignItems: 'center',
    zIndex: 100,
  },
  error:   { backgroundColor: Colors.destructive },
  success: { backgroundColor: Colors.emerald },
  text: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.primaryForeground,
    textAlign: 'center',
  },
});
