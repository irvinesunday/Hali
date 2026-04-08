/**
 * Hali design token — animation presets for Reanimated 2.
 *
 * Six animations ported from the web MVP's globals.css keyframes.
 * All use withTiming or withRepeat/withSequence from react-native-reanimated.
 *
 * Usage:
 *   const style = useAnimatedStyle(() => Animations.pulseSoft(opacity, scale));
 *
 * Each export is a hook-ready config object, not a pre-built hook.
 * Screens and components call useAnimatedStyle with these configs.
 */

import {
  withTiming,
  withRepeat,
  withSequence,
  Easing,
} from 'react-native-reanimated';

/**
 * pulse-soft — live indicator dot breathing.
 * opacity 1→0.5, scale 1→1.15, 2s infinite.
 * Used by: LiveDot component.
 */
export const pulseSoftConfig = {
  duration: 1000,
  easing: Easing.inOut(Easing.ease),
};

export function startPulseSoft(opacityValue: { value: number }, scaleValue: { value: number }) {
  opacityValue.value = withRepeat(
    withSequence(
      withTiming(0.5, pulseSoftConfig),
      withTiming(1, pulseSoftConfig),
    ),
    -1,
    true,
  );
  scaleValue.value = withRepeat(
    withSequence(
      withTiming(1.15, pulseSoftConfig),
      withTiming(1, pulseSoftConfig),
    ),
    -1,
    true,
  );
}

/**
 * count-pop — count value change feedback.
 * scale 1→1.15→1, 300ms.
 * Used by: animated count displays in ClusterDetail.
 */
export function triggerCountPop(scaleValue: { value: number }) {
  scaleValue.value = withSequence(
    withTiming(1.15, { duration: 150, easing: Easing.out(Easing.ease) }),
    withTiming(1, { duration: 150, easing: Easing.in(Easing.ease) }),
  );
}

/**
 * fade-up — element entering the feed.
 * opacity 0→1, translateY 8→0, 300ms ease-out.
 * Used by: ClusterCard, feed sections on mount.
 */
export const fadeUpConfig = {
  opacity: {
    from: 0,
    to: 1,
    duration: 300,
    easing: Easing.out(Easing.ease),
  },
  translateY: {
    from: 8,
    to: 0,
    duration: 300,
    easing: Easing.out(Easing.ease),
  },
};

export function startFadeUp(
  opacityValue: { value: number },
  translateYValue: { value: number },
) {
  opacityValue.value = withTiming(1, {
    duration: fadeUpConfig.opacity.duration,
    easing: fadeUpConfig.opacity.easing,
  });
  translateYValue.value = withTiming(0, {
    duration: fadeUpConfig.translateY.duration,
    easing: fadeUpConfig.translateY.easing,
  });
}

/**
 * modal-content — bottom sheet entrance.
 * opacity 0→1, translateY 24→0, scale 0.96→1, 300ms cubic.
 * Used by: OfficialUpdateSheet, LocalitySelector, FeedbackSheet.
 */
export const modalContentConfig = {
  duration: 300,
  easing: Easing.bezier(0.16, 1, 0.3, 1),
};

export function startModalContent(
  opacityValue: { value: number },
  translateYValue: { value: number },
  scaleValue: { value: number },
) {
  opacityValue.value = withTiming(1, modalContentConfig);
  translateYValue.value = withTiming(0, modalContentConfig);
  scaleValue.value = withTiming(1, modalContentConfig);
}

/**
 * breathe — FAB persistent breathing pulse.
 * scale 1→1.03→1, 3s infinite ease-in-out.
 * Used by: FAB component.
 */
export function startBreathe(scaleValue: { value: number }) {
  scaleValue.value = withRepeat(
    withSequence(
      withTiming(1.03, {
        duration: 1500,
        easing: Easing.inOut(Easing.ease),
      }),
      withTiming(1, {
        duration: 1500,
        easing: Easing.inOut(Easing.ease),
      }),
    ),
    -1,
    true,
  );
}

/**
 * slide-in-right — detail screen entrance.
 * opacity 0→1, translateX 16→0, 350ms cubic.
 * Used by: ClusterDetail section reveal.
 */
export const slideInRightConfig = {
  duration: 350,
  easing: Easing.bezier(0.16, 1, 0.3, 1),
};

export function startSlideInRight(
  opacityValue: { value: number },
  translateXValue: { value: number },
) {
  opacityValue.value = withTiming(1, slideInRightConfig);
  translateXValue.value = withTiming(0, slideInRightConfig);
}
