// apps/citizen-mobile/src/components/feed/ClusterCard.tsx
//
// List item for clusters in the home feed's four sections.
// Presentation-only: tap navigates to the cluster detail screen.
// No business logic, no data fetching.

import React from 'react';
import { TouchableOpacity, View, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { ClusterStateBadge } from '../clusters/ClusterStateBadge';
import { formatRelativeTime, formatCategoryLabel } from '../../utils/formatters';
import { Colors, FontFamily, FontSize, Spacing, Radius, Shadows } from '../../theme';
import type { ClusterResponse } from '../../types/api';

export interface ClusterCardProps {
  cluster: ClusterResponse;
}

export const ClusterCard = React.memo(
  function ClusterCard({ cluster }: ClusterCardProps): React.ReactElement {
    const router = useRouter();

    return (
      <TouchableOpacity
        style={styles.card}
        activeOpacity={0.8}
        onPress={() => router.push(`/(app)/clusters/${cluster.id}`)}
        accessible
        accessibilityRole="button"
        accessibilityLabel={cluster.title ?? formatCategoryLabel(cluster.category)}
        accessibilityHint="Open cluster details"
      >
        <View style={styles.header}>
          <Text style={styles.title} numberOfLines={2}>
            {cluster.title ?? formatCategoryLabel(cluster.category)}
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
  },
  (prev, next) =>
    prev.cluster.id === next.cluster.id &&
    prev.cluster.updatedAt === next.cluster.updatedAt &&
    prev.cluster.state === next.cluster.state &&
    prev.cluster.affectedCount === next.cluster.affectedCount &&
    prev.cluster.observingCount === next.cluster.observingCount &&
    prev.cluster.title === next.cluster.title &&
    prev.cluster.summary === next.cluster.summary &&
    prev.cluster.category === next.cluster.category,
);

const styles = StyleSheet.create({
  card: {
    backgroundColor: Colors.card,
    borderRadius: Radius.lg,
    padding: Spacing.lg,
    gap: Spacing.sm,
    borderWidth: 1,
    borderColor: Colors.border,
    ...Shadows.card,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: Spacing.sm,
  },
  title: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
    flex: 1,
  },
  summary: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    lineHeight: FontSize.body * 1.5,
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  meta: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  time: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
  },
});
