/**
 * Hali design token — animation presets for Reanimated 2.
 *
 * Five animations ported from the web MVP's globals.css keyframes.
 * All use withTiming or withRepeat/withSequence from react-native-reanimated.
 *
 * Usage — imperative helpers start or trigger animations on shared values:
 *   triggerCountPop(scale);
 *   startBreathe(scale);
 *
 * Separate *Config exports can be used when building animated styles:
 *   fadeUpConfig.opacity / fadeUpConfig.translateY
 *
 * This module exports imperative animation helpers (`start*` / `trigger*`)
 * plus separate `*Config` objects. It does not export pre-built hooks.
 */

import {
  withTiming,
  withRepeat,
  withSequence,
  Easing,
  type SharedValue,
} from 'react-native-reanimated';

/**
 * count-pop — count value change feedback.
 * scale 1→1.15→1, 300ms.
 * Used by: animated count displays in ClusterDetail.
 */
export function triggerCountPop(scaleValue: SharedValue<number>) {
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
  opacityValue: SharedValue<number>,
  translateYValue: SharedValue<number>,
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
  opacityValue: SharedValue<number>,
  translateYValue: SharedValue<number>,
  scaleValue: SharedValue<number>,
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
export function startBreathe(scaleValue: SharedValue<number>) {
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
  opacityValue: SharedValue<number>,
  translateXValue: SharedValue<number>,
) {
  opacityValue.value = withTiming(1, slideInRightConfig);
  translateXValue.value = withTiming(0, slideInRightConfig);
}
