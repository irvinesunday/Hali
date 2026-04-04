import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import type { ClusterState } from '../../types/api';

interface ClusterStateBadgeProps {
  state: ClusterState;
}

const STATE_LABELS: Record<ClusterState, string> = {
  unconfirmed: 'Unconfirmed',
  active: 'Active Now',
  possible_restoration: 'Possible Restoration',
  resolved: 'Resolved',
  recurring_context: 'Recurring',
};

const STATE_COLORS: Record<ClusterState, { bg: string; text: string }> = {
  unconfirmed: { bg: '#fef3c7', text: '#92400e' },
  active: { bg: '#dcfce7', text: '#166534' },
  possible_restoration: { bg: '#dbeafe', text: '#1e40af' },
  resolved: { bg: '#f3f4f6', text: '#6b7280' },
  recurring_context: { bg: '#ede9fe', text: '#5b21b6' },
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
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  text: { fontSize: 12, fontWeight: '600' },
});
