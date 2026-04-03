import React from 'react';
import { TouchableOpacity, View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { ClusterStateBadge } from './ClusterStateBadge';
import { formatRelativeTime, formatCategoryLabel } from '../../utils/formatters';
import type { ClusterResponse } from '../../types/api';

interface ClusterCardProps {
  cluster: ClusterResponse;
}

export function ClusterCard({ cluster }: ClusterCardProps) {
  const router = useRouter();

  return (
    <TouchableOpacity
      style={styles.card}
      activeOpacity={0.8}
      onPress={() => router.push(`/(app)/clusters/${cluster.id}`)}
    >
      <View style={styles.header}>
        <Text style={styles.category}>
          {formatCategoryLabel(cluster.category)}
        </Text>
        <ClusterStateBadge state={cluster.state} />
      </View>

      {cluster.summary ? (
        <Text style={styles.summary} numberOfLines={2}>
          {cluster.summary}
        </Text>
      ) : null}

      <View style={styles.footer}>
        <Text style={styles.meta}>
          {cluster.affectedCount} affected · {cluster.observingCount} observing
        </Text>
        <Text style={styles.time}>{formatRelativeTime(cluster.updatedAt)}</Text>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: '#fff',
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
  category: { fontSize: 15, fontWeight: '600', color: '#111827', flex: 1 },
  summary: { fontSize: 14, color: '#374151', lineHeight: 20 },
  footer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  meta: { fontSize: 13, color: '#6b7280' },
  time: { fontSize: 12, color: '#9ca3af' },
});
