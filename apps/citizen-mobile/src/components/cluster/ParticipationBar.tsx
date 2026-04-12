// apps/citizen-mobile/src/components/cluster/ParticipationBar.tsx
//
// Affected/Observing count display for the cluster detail screen.
// Pure presentation — receives counts as props, never fetches.
//
// This is the visual stat row at the top of the detail screen
// (NOT the action buttons — those live inline in cluster/[id].tsx
// because they need access to local participation state and the 2-min
// context window timer).

import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Colors, FontFamily, FontSize, Spacing, Radius } from '../../theme';

export interface ParticipationBarProps {
  affectedCount: number;
  observingCount: number;
}

export function ParticipationBar({
  affectedCount,
  observingCount,
}: ParticipationBarProps): React.ReactElement {
  return (
    <View
      style={styles.container}
      accessible
      accessibilityRole="summary"
      accessibilityLabel={`${affectedCount} people affected, ${observingCount} people observing`}
    >
      <View style={styles.stat}>
        <Text style={styles.count}>{affectedCount}</Text>
        <Text style={styles.label}>Affected</Text>
      </View>
      <View style={styles.divider} />
      <View style={styles.stat}>
        <Text style={styles.count}>{observingCount}</Text>
        <Text style={styles.label}>Observing</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    backgroundColor: Colors.muted,
    borderRadius: Radius.md,
    padding: Spacing.lg,
    gap: Spacing.lg,
  },
  stat:    { flex: 1, alignItems: 'center' },
  count: {
    fontSize: 22,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  label: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    marginTop: 2,
  },
  divider: { width: 1, backgroundColor: Colors.border },
});
