import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Colors, FontFamily, FontSize, Radius, Spacing } from '../../theme';
import type { ClusterState } from '../../types/api';

interface ClusterStateBadgeProps {
  state: ClusterState;
}

const STATE_LABELS: Record<ClusterState, string> = {
  unconfirmed:          'Unconfirmed',
  active:               'Active Now',
  possible_restoration: 'Possible Restoration',
  resolved:             'Resolved',
  recurring_context:    'Recurring',
};

const STATE_COLORS: Record<ClusterState, { bg: string; text: string }> = {
  unconfirmed:          { bg: '#FEF9C3', text: '#854D0E' },
  active:               { bg: Colors.primarySubtle, text: Colors.primary },
  possible_restoration: { bg: Colors.emeraldSubtle, text: Colors.emerald },
  resolved:             { bg: Colors.muted, text: Colors.mutedForeground },
  recurring_context:    { bg: '#EDE9FE', text: '#5B21B6' },
};

export function ClusterStateBadge({ state }: ClusterStateBadgeProps) {
  const colors = STATE_COLORS[state] ?? STATE_COLORS.unconfirmed;
  return (
    <View style={[styles.badge, { backgroundColor: colors.bg }]}>
      <Text style={[styles.text, { color: colors.text }]}>
        {STATE_LABELS[state] ?? state}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: 'flex-start',
    borderRadius: Radius.xs,
    paddingHorizontal: Spacing.sm,
    paddingVertical: 3,
  },
  text: {
    fontSize: FontSize.badge,
    fontFamily: FontFamily.semiBold,
  },
});
