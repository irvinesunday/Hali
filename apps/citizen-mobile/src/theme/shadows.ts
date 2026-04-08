/**
 * Hali design token — shadow definitions.
 * React Native shadows require separate iOS / Android treatment.
 * Use the spread operator to apply: { ...Shadows.card }
 */

import { Platform } from 'react-native';

const iosShadow = (
  opacity: number,
  radius: number,
  offsetY: number,
) => ({
  shadowColor: '#000',
  shadowOffset: { width: 0, height: offsetY },
  shadowOpacity: opacity,
  shadowRadius: radius,
});

const androidElevation = (elevation: number) => ({
  elevation,
});

function shadow(opacity: number, radius: number, offsetY: number, elevation: number) {
  return Platform.OS === 'ios'
    ? iosShadow(opacity, radius, offsetY)
    : androidElevation(elevation);
}

export const Shadows = {
  /** Subtle card resting shadow */
  card: shadow(0.06, 3, 1, 2),
  /** Card hover / elevated state */
  cardElevated: shadow(0.10, 6, 3, 4),
  /** FAB shadow */
  fab: shadow(0.15, 8, 4, 6),
  /** Modal / sheet shadow */
  modal: shadow(0.20, 16, 8, 10),
} as const;
