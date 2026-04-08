import React, { useEffect } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
} from 'react-native-reanimated';
import { startPulseSoft } from '../../theme';
import { Colors, FontFamily, FontSize } from '../../theme';

type LiveIntensity = 'strong' | 'soft' | 'dim';

interface LiveDotProps {
  intensity?: LiveIntensity;
}

// Append opacity hex suffix to primary colour
const DOT_COLOR: Record<LiveIntensity, string> = {
  strong: Colors.primary,
  soft:   Colors.primary + 'B3',  // 70%
  dim:    Colors.primary + '80',  // 50%
};

export function LiveDot({ intensity = 'soft' }: LiveDotProps) {
  const opacity = useSharedValue(1);
  const scale   = useSharedValue(1);

  useEffect(() => {
    if (intensity !== 'dim') {
      startPulseSoft(opacity, scale);
    }
  }, [intensity]);

  const animatedStyle = useAnimatedStyle(() => ({
    opacity: opacity.value,
    transform: [{ scale: scale.value }],
  }));

  return (
    <View style={styles.row}>
      <Animated.View
        style={[
          styles.dot,
          { backgroundColor: DOT_COLOR[intensity] },
          intensity !== 'dim' && animatedStyle,
        ]}
      />
      <Text style={[styles.label, { color: DOT_COLOR[intensity] }]}>
        Live
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 4 },
  dot: { width: 6, height: 6, borderRadius: 3 },
  label: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.medium,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
});
