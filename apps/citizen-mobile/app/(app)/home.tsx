// Home Feed — four sections, ward selector, floating Report button
import React, { useState } from 'react';
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
import { ClusterCard } from '../../src/components/clusters/ClusterCard';
import { Empty } from '../../src/components/common/Empty';
import { Loading } from '../../src/components/common/Loading';
import { getHome } from '../../src/api/clusters';
import { getFollowedLocalities } from '../../src/api/localities';
import { useLocalityContext } from '../../src/context/LocalityContext';
import type { ClusterResponse, OfficialPostResponse } from '../../src/types/api';

export default function HomeScreen() {
  const router = useRouter();
  const { activeLocalityId, followedLocalityIds, setActiveLocalityId, setFollowedLocalityIds } =
    useLocalityContext();
  const [wardPickerVisible, setWardPickerVisible] = useState(false);

  // Seed followed wards on mount
  const localitiesQuery = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: async () => {
      const res = await getFollowedLocalities();
      setFollowedLocalityIds(res.localityIds);
      return res;
    },
    staleTime: 60_000,
  });

  const homeQuery = useQuery({
    queryKey: ['home', activeLocalityId],
    queryFn: () => getHome(activeLocalityId!),
    enabled: !!activeLocalityId,
    staleTime: 30_000,
  });

  const isLoading = homeQuery.isLoading && !homeQuery.data;
  const feed = homeQuery.data;

  const allEmpty =
    feed &&
    feed.activeNow.length === 0 &&
    feed.officialUpdates.length === 0 &&
    feed.recurringAtThisTime.length === 0 &&
    feed.otherActiveSignals.length === 0;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.wordmark}>hali</Text>
        <TouchableOpacity
          style={styles.wardPill}
          onPress={() => setWardPickerVisible(true)}
        >
          <Text style={styles.wardPillText} numberOfLines={1}>
            {activeLocalityId
              ? `Ward: ${activeLocalityId.slice(0, 8)}…`
              : 'Select ward'}
          </Text>
          <Ionicons name="chevron-down" size={14} color="#1a3a2f" />
        </TouchableOpacity>
      </View>

      {isLoading ? (
        <Loading />
      ) : !activeLocalityId ? (
        <View style={styles.center}>
          <Empty
            message="No ward selected"
            subMessage="Go to Settings → Wards to follow a ward."
          />
        </View>
      ) : allEmpty ? (
        <View style={styles.center}>
          <Empty
            message="Currently calm in this ward"
            subMessage="No active signals right now."
          />
        </View>
      ) : (
        <ScrollView
          style={styles.flex}
          contentContainerStyle={styles.scrollContent}
          refreshControl={
            <RefreshControl
              refreshing={homeQuery.isFetching}
              onRefresh={() => homeQuery.refetch()}
              tintColor="#1a3a2f"
            />
          }
        >
          {/* Section 1 — Active now */}
          <Section
            title="Active now"
            clusters={feed?.activeNow ?? []}
            emptyMessage="No active signals right now."
          />

          {/* Section 2 — Official updates */}
          {(feed?.officialUpdates ?? []).length > 0 && (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Official updates</Text>
              {feed!.officialUpdates.map((post) => (
                <OfficialPostCard key={post.id} post={post} />
              ))}
            </View>
          )}
          {(feed?.officialUpdates ?? []).length === 0 && (
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Official updates</Text>
              <Empty message="No official updates." />
            </View>
          )}

          {/* Section 3 — Recurring */}
          <Section
            title="Recurring at this time"
            clusters={feed?.recurringAtThisTime ?? []}
            emptyMessage="No recurring signals."
          />

          {/* Section 4 — Other active */}
          <Section
            title="Other active signals"
            clusters={feed?.otherActiveSignals ?? []}
            emptyMessage="Nothing else active."
          />
        </ScrollView>
      )}

      {/* Floating Report button */}
      <TouchableOpacity
        style={styles.fab}
        activeOpacity={0.85}
        onPress={() => router.push('/(app)/compose/text')}
      >
        <Ionicons name="add" size={28} color="#fff" />
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
            {followedLocalityIds.length === 0 ? (
              <Empty
                message="No wards followed"
                subMessage="Go to Settings → Wards to follow up to 5 wards."
              />
            ) : (
              <FlatList
                data={followedLocalityIds}
                keyExtractor={(item) => item}
                renderItem={({ item }) => (
                  <TouchableOpacity
                    style={[
                      styles.wardRow,
                      item === activeLocalityId && styles.wardRowActive,
                    ]}
                    onPress={() => {
                      setActiveLocalityId(item);
                      setWardPickerVisible(false);
                    }}
                  >
                    <Text style={styles.wardRowText}>
                      Ward {item.slice(0, 8)}…
                    </Text>
                    {item === activeLocalityId && (
                      <Ionicons name="checkmark" size={18} color="#1a3a2f" />
                    )}
                  </TouchableOpacity>
                )}
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

function Section({
  title,
  clusters,
  emptyMessage,
}: {
  title: string;
  clusters: ClusterResponse[];
  emptyMessage: string;
}) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      {clusters.length === 0 ? (
        <Empty message={emptyMessage} />
      ) : (
        clusters.map((c) => <ClusterCard key={c.id} cluster={c} />)
      )}
    </View>
  );
}

function OfficialPostCard({ post }: { post: OfficialPostResponse }) {
  return (
    <View style={styles.officialCard}>
      <Text style={styles.officialTitle}>{post.title}</Text>
      <Text style={styles.officialBody} numberOfLines={3}>
        {post.body}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#f9fafb' },
  flex: { flex: 1 },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  wordmark: { fontSize: 22, fontWeight: '800', color: '#1a3a2f' },
  wardPill: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f0fdf4',
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
    backgroundColor: '#fffbeb',
    borderRadius: 12,
    padding: 14,
    gap: 6,
    borderLeftWidth: 3,
    borderLeftColor: '#f59e0b',
  },
  officialTitle: { fontSize: 15, fontWeight: '600', color: '#111827' },
  officialBody: { fontSize: 14, color: '#374151', lineHeight: 20 },
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
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
    justifyContent: 'flex-end',
  },
  modalSheet: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    padding: 20,
    maxHeight: '60%',
    gap: 12,
  },
  modalTitle: { fontSize: 18, fontWeight: '700', color: '#111827' },
  wardRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 14,
    borderBottomWidth: 1,
    borderBottomColor: '#f3f4f6',
  },
  wardRowActive: { backgroundColor: '#f0fdf4' },
  wardRowText: { fontSize: 15, color: '#111827' },
  modalClose: { paddingVertical: 12, alignItems: 'center' },
  modalCloseText: { fontSize: 15, color: '#6b7280' },
});
