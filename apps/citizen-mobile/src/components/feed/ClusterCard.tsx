// apps/citizen-mobile/src/components/feed/ClusterCard.tsx
//
// List item for clusters in the home feed's four sections.
// Presentation-only: tap navigates to the cluster detail screen.
// No business logic, no data fetching.

import React from 'react';
import { TouchableOpacity, View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { ClusterStateBadge } from '../clusters/ClusterStateBadge';
import {
  formatRelativeTime,
  formatCategoryLabel,
} from '../../utils/formatters';
import type { ClusterResponse } from '../../types/api';

export interface ClusterCardProps {
  cluster: ClusterResponse;
}

export function ClusterCard({ cluster }: ClusterCardProps): React.ReactElement {
  const router = useRouter();

  const onPress = (): void => {
    router.push(`/(app)/clusters/${cluster.id}`);
  };

  return (
    <TouchableOpacity
      style={styles.card}
      activeOpacity={0.8}
      onPress={onPress}
      accessible
      accessibilityRole="button"
      accessibilityLabel={
        cluster.title ?? formatCategoryLabel(cluster.category)
      }
      accessibilityHint="Open cluster details"
    >
      <View style={styles.header}>
        <Text style={styles.category} numberOfLines={1}>
          {cluster.title ?? formatCategoryLabel(cluster.category)}
        </Text>
        <ClusterStateBadge state={cluster.state} />
      </View>

      {cluster.summary !== null && cluster.summary !== '' ? (
        <Text style={styles.summary} numberOfLines={2}>
          {cluster.summary}
        </Text>
      ) : null}

      <View style={styles.footer}>
        <Text style={styles.meta}>
          {cluster.affectedCount} affected · {cluster.observingCount} observing
        </Text>
        <Text style={styles.time}>
          {formatRelativeTime(cluster.updatedAt)}
        </Text>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 14,
    gap: 8,
    shadowColor: '#000',
    shadowOpacity: 0.06,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 2 },
    elevation: 2,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: 8,
  },
  category: {
    fontSize: 15,
    fontWeight: '600',
    color: '#111827',
    flex: 1,
  },
  summary: {
    fontSize: 14,
    color: '#374151',
    lineHeight: 20,
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  meta: { fontSize: 13, color: '#6B7280' },
  time: { fontSize: 12, color: '#9CA3AF' },
});
