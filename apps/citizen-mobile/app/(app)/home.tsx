// apps/citizen-mobile/app/(app)/home.tsx
//
// Home feed — four canonical sections + calm state + persistent Report FAB.
//
// Section order (canonical, must not be reordered):
//   1. Active now
//   2. Official updates
//   3. Recurring at this time
//   4. Other active signals
//
// Calm state rules (see docs/arch/04_phase1_mobile.md):
//   - When ALL four sections have items.length === 0, show:
//       "Currently calm in your followed wards" + last-checked timestamp
//   - When the user has no follows at all, show a different "follow a ward"
//     empty state — calm only applies when you're actively watching wards
//     and nothing is happening.
//   - Scheduled / upcoming official updates still render even when calm.
//   - Never show a generic empty illustration.

import React, { useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  RefreshControl,
  TouchableOpacity,
  StyleSheet,
  Modal,
  FlatList,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery } from '@tanstack/react-query';
import { ClusterCard } from '../../src/components/feed/ClusterCard';
import { Loading } from '../../src/components/common/Loading';
import { getFollowedLocalities } from '../../src/api/localities';
import { useHome, ApiResultError } from '../../src/hooks/useClusters';
import { useLocalityContext } from '../../src/context/LocalityContext';
import { formatRelativeTime } from '../../src/utils/formatters';
import type {
  ClusterResponse,
  OfficialPostResponse,
  PagedSection,
} from '../../src/types/api';

// ─── Section presentation ────────────────────────────────────────────────────

function isSectionEmpty<T>(section: PagedSection<T> | undefined): boolean {
  return !section || section.items.length === 0;
}

// ─── Component ───────────────────────────────────────────────────────────────

export default function HomeScreen(): React.ReactElement {
  const router = useRouter();
  const {
    activeLocality,
    followedLocalities,
    followsLoaded,
    setActiveLocalityId,
    setFollowedLocalities,
  } = useLocalityContext();
  const [wardPickerVisible, setWardPickerVisible] = useState(false);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  // ── Load followed localities once on mount, store in context ─────────────
  // getFollowedLocalities returns Result<T, ApiError>; unwrap inside the
  // queryFn so the surrounding hook stays a normal TanStack query.
  const localitiesQuery = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: async () => {
      const result = await getFollowedLocalities();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
    staleTime: 60_000,
  });

  useEffect(() => {
    if (localitiesQuery.data) {
      setFollowedLocalities(localitiesQuery.data);
    }
  }, [localitiesQuery.data, setFollowedLocalities]);

  // ── Home feed query — NOT scoped by activeLocalityId ─────────────────────
  // The backend merges data across all follows. activeLocalityId is
  // client-side only.
  const homeQuery = useHome();

  useEffect(() => {
    if (homeQuery.dataUpdatedAt > 0) {
      setLastUpdatedAt(new Date(homeQuery.dataUpdatedAt));
    }
  }, [homeQuery.dataUpdatedAt]);

  // ── Derived state ────────────────────────────────────────────────────────
  const feed = homeQuery.data;

  const isCalmState = useMemo<boolean>(() => {
    if (!feed) return false;
    return (
      isSectionEmpty(feed.activeNow) &&
      isSectionEmpty(feed.officialUpdates) &&
      isSectionEmpty(feed.recurringAtThisTime) &&
      isSectionEmpty(feed.otherActiveSignals)
    );
  }, [feed]);

  const hasNoFollows = followsLoaded && followedLocalities.length === 0;

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.wordmark}>hali</Text>
        <TouchableOpacity
          style={styles.wardPill}
          onPress={() => setWardPickerVisible(true)}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Ward selector"
          disabled={followedLocalities.length === 0}
        >
          <Text style={styles.wardPillText} numberOfLines={1}>
            {activeLocality
              ? (activeLocality.displayLabel ?? activeLocality.wardName)
              : 'No ward selected'}
          </Text>
          <Ionicons name="chevron-down" size={14} color="#1a3a2f" />
        </TouchableOpacity>
      </View>

      {homeQuery.isLoading && !feed ? (
        <Loading />
      ) : homeQuery.isError ? (
        <View style={styles.center}>
          <ErrorState
            message={(homeQuery.error as ApiResultError).message}
            onRetry={() => void homeQuery.refetch()}
          />
        </View>
      ) : (
        <ScrollView
          style={styles.flex}
          contentContainerStyle={styles.scrollContent}
          refreshControl={
            <RefreshControl
              refreshing={homeQuery.isFetching && !homeQuery.isLoading}
              onRefresh={() => void homeQuery.refetch()}
              tintColor="#1a3a2f"
            />
          }
        >
          {hasNoFollows ? (
            <NoFollowsState onOpenSettings={() => router.push('/(app)/settings/wards')} />
          ) : isCalmState ? (
            <CalmState
              followedCount={followedLocalities.length}
              lastUpdatedAt={lastUpdatedAt}
            />
          ) : (
            <>
              <Section
                title="Active now"
                clusters={feed?.activeNow.items ?? []}
              />
              <OfficialUpdatesSection
                posts={feed?.officialUpdates.items ?? []}
              />
              <Section
                title="Recurring at this time"
                clusters={feed?.recurringAtThisTime.items ?? []}
              />
              <Section
                title="Other active signals"
                clusters={feed?.otherActiveSignals.items ?? []}
              />
              {lastUpdatedAt !== null && (
                <Text style={styles.lastUpdated}>
                  Last updated {formatRelativeTime(lastUpdatedAt.toISOString())}
                </Text>
              )}
            </>
          )}
        </ScrollView>
      )}

      {/* Persistent Report FAB — always visible, even in calm/no-follows */}
      <TouchableOpacity
        style={styles.fab}
        activeOpacity={0.85}
        onPress={() => router.push('/(app)/compose/text')}
        accessible
        accessibilityRole="button"
        accessibilityLabel="Report a signal"
      >
        <Ionicons name="add" size={28} color="#FFFFFF" />
      </TouchableOpacity>

      {/* Ward picker modal */}
      <Modal
        visible={wardPickerVisible}
        transparent
        animationType="slide"
        onRequestClose={() => setWardPickerVisible(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalSheet}>
            <Text style={styles.modalTitle}>Select ward</Text>
            {followedLocalities.length === 0 ? (
              <Text style={styles.modalEmpty}>
                You haven't followed any wards yet. Go to Settings → Wards to
                follow up to 5 wards.
              </Text>
            ) : (
              <FlatList
                data={followedLocalities}
                keyExtractor={(item) => item.localityId}
                renderItem={({ item }) => {
                  const isActive =
                    item.localityId === activeLocality?.localityId;
                  return (
                    <TouchableOpacity
                      style={[
                        styles.wardRow,
                        isActive && styles.wardRowActive,
                      ]}
                      onPress={() => {
                        setActiveLocalityId(item.localityId);
                        setWardPickerVisible(false);
                      }}
                    >
                      <Text style={styles.wardRowText}>
                        {item.displayLabel ?? item.wardName}
                      </Text>
                      {isActive && (
                        <Ionicons
                          name="checkmark"
                          size={18}
                          color="#1a3a2f"
                        />
                      )}
                    </TouchableOpacity>
                  );
                }}
              />
            )}
            <TouchableOpacity
              style={styles.modalClose}
              onPress={() => setWardPickerVisible(false)}
            >
              <Text style={styles.modalCloseText}>Close</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function Section({
  title,
  clusters,
}: {
  title: string;
  clusters: ClusterResponse[];
}): React.ReactElement | null {
  if (clusters.length === 0) return null;
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      {clusters.map((c) => (
        <ClusterCard key={c.id} cluster={c} />
      ))}
    </View>
  );
}

function OfficialUpdatesSection({
  posts,
}: {
  posts: OfficialPostResponse[];
}): React.ReactElement | null {
  if (posts.length === 0) return null;
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Official updates</Text>
      {posts.map((post) => (
        <OfficialPostCard key={post.id} post={post} />
      ))}
    </View>
  );
}

function OfficialPostCard({
  post,
}: {
  post: OfficialPostResponse;
}): React.ReactElement {
  return (
    <View style={styles.officialCard}>
      <Text style={styles.officialTitle}>{post.title}</Text>
      <Text style={styles.officialBody} numberOfLines={3}>
        {post.body}
      </Text>
    </View>
  );
}

function CalmState({
  followedCount,
  lastUpdatedAt,
}: {
  followedCount: number;
  lastUpdatedAt: Date | null;
}): React.ReactElement {
  return (
    <View style={styles.calmContainer}>
      <Text style={styles.calmTitle}>
        Currently calm in your followed {followedCount === 1 ? 'ward' : 'wards'}
      </Text>
      <Text style={styles.calmBody}>
        No active signals, official updates, or recurring patterns right now.
      </Text>
      {lastUpdatedAt !== null && (
        <Text style={styles.calmTimestamp}>
          Last checked {formatRelativeTime(lastUpdatedAt.toISOString())}
        </Text>
      )}
    </View>
  );
}

function NoFollowsState({
  onOpenSettings,
}: {
  onOpenSettings: () => void;
}): React.ReactElement {
  return (
    <View style={styles.calmContainer}>
      <Text style={styles.calmTitle}>Follow a ward to see activity</Text>
      <Text style={styles.calmBody}>
        Hali shows you civic signals in the wards you follow. Pick up to 5.
      </Text>
      <TouchableOpacity
        style={styles.primaryCta}
        onPress={onOpenSettings}
        accessible
        accessibilityRole="button"
      >
        <Text style={styles.primaryCtaText}>Manage wards</Text>
      </TouchableOpacity>
    </View>
  );
}

function ErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}): React.ReactElement {
  return (
    <View style={styles.calmContainer}>
      <Text style={styles.calmTitle}>Couldn't load the feed</Text>
      <Text style={styles.calmBody}>{message}</Text>
      <TouchableOpacity
        style={styles.primaryCta}
        onPress={onRetry}
        accessible
        accessibilityRole="button"
      >
        <Text style={styles.primaryCtaText}>Try again</Text>
      </TouchableOpacity>
    </View>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F9FAFB' },
  flex: { flex: 1 },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: '#E5E7EB',
  },
  wordmark: { fontSize: 22, fontWeight: '800', color: '#1a3a2f' },
  wardPill: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0FDF4',
    borderRadius: 20,
    paddingHorizontal: 12,
    paddingVertical: 6,
    gap: 4,
    maxWidth: 180,
  },
  wardPillText: { fontSize: 13, color: '#1a3a2f', fontWeight: '500' },
  scrollContent: { padding: 16, gap: 8, paddingBottom: 100 },
  section: { gap: 10, marginBottom: 8 },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#111827',
    marginBottom: 2,
  },
  officialCard: {
    backgroundColor: '#FFFBEB',
    borderRadius: 12,
    padding: 14,
    gap: 6,
    borderLeftWidth: 3,
    borderLeftColor: '#F59E0B',
  },
  officialTitle: { fontSize: 15, fontWeight: '600', color: '#111827' },
  officialBody: { fontSize: 14, color: '#374151', lineHeight: 20 },
  lastUpdated: {
    fontSize: 12,
    color: '#9CA3AF',
    textAlign: 'center',
    marginTop: 16,
  },
  fab: {
    position: 'absolute',
    bottom: 32,
    right: 20,
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#1a3a2f',
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#000',
    shadowOpacity: 0.2,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 4 },
    elevation: 6,
  },
  center: { flex: 1, justifyContent: 'center' },
  calmContainer: {
    padding: 24,
    gap: 10,
    alignItems: 'flex-start',
  },
  calmTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
  },
  calmBody: {
    fontSize: 14,
    color: '#6B7280',
    lineHeight: 20,
  },
  calmTimestamp: {
    fontSize: 12,
    color: '#9CA3AF',
    marginTop: 4,
  },
  primaryCta: {
    marginTop: 12,
    backgroundColor: '#1a3a2f',
    paddingVertical: 12,
    paddingHorizontal: 20,
    borderRadius: 10,
  },
  primaryCtaText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
    justifyContent: 'flex-end',
  },
  modalSheet: {
    backgroundColor: '#FFFFFF',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    padding: 20,
    maxHeight: '60%',
    gap: 12,
  },
  modalTitle: { fontSize: 18, fontWeight: '700', color: '#111827' },
  modalEmpty: {
    fontSize: 14,
    color: '#6B7280',
    lineHeight: 20,
    paddingVertical: 12,
  },
  wardRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: '#F3F4F6',
  },
  wardRowActive: { backgroundColor: '#F0FDF4' },
  wardRowText: { fontSize: 15, color: '#111827' },
  modalClose: { paddingVertical: 12, alignItems: 'center' },
  modalCloseText: { fontSize: 15, color: '#6B7280' },
});
