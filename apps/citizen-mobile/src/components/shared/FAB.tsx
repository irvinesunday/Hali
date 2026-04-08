import React, { useEffect } from 'react';
import { TouchableOpacity, StyleSheet, AccessibilityInfo } from 'react-native';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  cancelAnimation,
  withTiming,
} from 'react-native-reanimated';
import { Plus } from 'lucide-react-native';
import { startBreathe, Colors, Shadows, Spacing } from '../../theme';

interface FABProps {
  onPress: () => void;
}

export function FAB({ onPress }: FABProps) {
  const scale = useSharedValue(1);

  useEffect(() => {
    let cancelled = false;
    AccessibilityInfo.isReduceMotionEnabled().then((reduceMotion) => {
      if (!reduceMotion && !cancelled) startBreathe(scale);
    });
    return () => {
      cancelled = true;
      cancelAnimation(scale);
      scale.value = withTiming(1, { duration: 150 });
    };
  }, []);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ scale: scale.value }],
  }));

  return (
    <Animated.View style={[styles.wrapper, animatedStyle]}>
      <TouchableOpacity
        style={styles.button}
        onPress={onPress}
        activeOpacity={0.85}
        accessibilityRole="button"
        accessibilityLabel="Report a new signal"
      >
        <Plus size={24} color={Colors.primaryForeground} strokeWidth={2.5} />
      </TouchableOpacity>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  wrapper: {
    position: 'absolute',
    bottom: 84,
    right: Spacing.lg,
    zIndex: 40,
  },
  button: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: Colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
    ...Shadows.fab,
  },
});
