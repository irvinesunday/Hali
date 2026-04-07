// apps/citizen-mobile/app/(app)/settings/wards.tsx
//
// Ward following — view, search, add, remove. Max 5 enforced both
// client-side (search disabled at capacity) and server-side
// (422 max_followed_localities_exceeded).
//
// PUT /v1/localities/followed replaces the full set, so add and remove
// both send the entire updated array of items in one call.

import React, { useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  TextInput,
  ActivityIndicator,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getFollowedLocalities,
  searchLocalities,
  setFollowedLocalities,
} from '../../../src/api/localities';
import { useLocalityContext } from '../../../src/context/LocalityContext';
import { Loading } from '../../../src/components/common/Loading';
import { MAX_FOLLOWED_WARDS } from '../../../src/config/constants';
import type {
  ApiError,
  FollowedLocality,
  FollowedLocalityItem,
  LocalitySearchResult,
} from '../../../src/types/api';

const SEARCH_DEBOUNCE_MS = 400;

export default function WardsSettingsScreen(): React.ReactElement {
  const router = useRouter();
  const qc = useQueryClient();
  const { setFollowedLocalities: pushContextFollowed } = useLocalityContext();

  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [toast, setToast] = useState<string | null>(null);

  // ── Followed localities query ─────────────────────────────────────────────
  const followedQuery = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: async () => {
      const result = await getFollowedLocalities();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
  });

  const current = useMemo<FollowedLocality[]>(
    () => followedQuery.data ?? [],
    [followedQuery.data],
  );

  // ── Debounced search ──────────────────────────────────────────────────────
  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(query.trim()), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(t);
  }, [query]);

  const atCapacity = current.length >= MAX_FOLLOWED_WARDS;

  const searchQuery = useQuery({
    queryKey: ['localities', 'search', debouncedQuery],
    queryFn: async () => {
      const result = await searchLocalities(debouncedQuery);
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
    enabled: debouncedQuery.length >= 2 && !atCapacity,
    staleTime: 60_000,
  });

  // ── Update mutation ───────────────────────────────────────────────────────
  const updateMutation = useMutation<void, Error, FollowedLocalityItem[]>({
    mutationFn: async (items) => {
      const result = await setFollowedLocalities({ items });
      if (!result.ok) throw new ApiResultError(result.error);
    },
    onSuccess: async () => {
      const refreshed = await getFollowedLocalities();
      if (refreshed.ok) {
        pushContextFollowed(refreshed.value);
      }
      qc.invalidateQueries({ queryKey: ['localities', 'followed'] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
    onError: (err) => {
      if (err instanceof ApiResultError) {
        if (err.apiError.code === 'max_followed_localities_exceeded') {
          setToast(`You can follow up to ${MAX_FOLLOWED_WARDS} areas.`);
          return;
        }
        setToast(err.apiError.message);
        return;
      }
      setToast('Could not update followed wards. Please try again.');
    },
  });

  function handleAdd(candidate: LocalitySearchResult): void {
    if (current.some((c) => c.localityId === candidate.localityId)) {
      setToast('You are already following this area.');
      return;
    }
    if (atCapacity) {
      setToast(`You can follow up to ${MAX_FOLLOWED_WARDS} areas.`);
      return;
    }
    setToast(null);
    const items: FollowedLocalityItem[] = [
      ...current.map((c) => ({
        localityId: c.localityId,
        displayLabel: c.displayLabel,
      })),
      { localityId: candidate.localityId, displayLabel: candidate.placeLabel },
    ];
    updateMutation.mutate(items);
    setQuery('');
    setDebouncedQuery('');
  }

  function handleRemove(localityId: string): void {
    setToast(null);
    const items: FollowedLocalityItem[] = current
      .filter((c) => c.localityId !== localityId)
      .map((c) => ({ localityId: c.localityId, displayLabel: c.displayLabel }));
    updateMutation.mutate(items);
  }

  if (followedQuery.isLoading && !followedQuery.data) {
    return <Loading />;
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Back"
        >
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Ward following</Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View
          style={[styles.badge, atCapacity && styles.badgeAtCapacity]}
          accessible
          accessibilityRole="text"
          accessibilityLabel={`${current.length} of ${MAX_FOLLOWED_WARDS} areas followed`}
        >
          <Text
            style={[
              styles.badgeText,
              atCapacity && styles.badgeTextAtCapacity,
            ]}
          >
            {current.length} of {MAX_FOLLOWED_WARDS} areas followed
          </Text>
        </View>

        {current.length === 0 && (
          <Text style={styles.empty}>
            You&apos;re not following any areas yet. Search below to find an
            area or estate to follow.
          </Text>
        )}

        {current.map((item) => (
          <View key={item.localityId} style={styles.wardRow}>
            <View style={styles.wardRowText}>
              <Text style={styles.wardName} numberOfLines={1}>
                {item.displayLabel ?? item.wardName}
              </Text>
              {item.cityName !== null && (
                <Text style={styles.wardCity} numberOfLines={1}>
                  {item.wardName}
                  {item.cityName ? ` · ${item.cityName}` : ''}
                </Text>
              )}
            </View>
            <TouchableOpacity
              onPress={() => handleRemove(item.localityId)}
              hitSlop={8}
              disabled={updateMutation.isPending}
              accessible
              accessibilityRole="button"
              accessibilityLabel={`Remove ${item.displayLabel ?? item.wardName}`}
            >
              <Ionicons name="close-circle" size={22} color="#DC2626" />
            </TouchableOpacity>
          </View>
        ))}

        {atCapacity ? (
          <Text style={styles.capacityHint}>
            You can follow up to {MAX_FOLLOWED_WARDS} areas. Remove one to add
            another.
          </Text>
        ) : (
          <>
            <Text style={styles.sectionLabel}>Add an area</Text>
            <TextInput
              style={styles.searchInput}
              value={query}
              onChangeText={setQuery}
              placeholder="Search for an area or estate..."
              placeholderTextColor="#9CA3AF"
              autoCapitalize="words"
              autoCorrect={false}
              editable={!updateMutation.isPending}
              accessible
              accessibilityLabel="Area search"
            />

            {searchQuery.isFetching && (
              <ActivityIndicator color="#1a3a2f" style={{ marginTop: 8 }} />
            )}

            {searchQuery.data && searchQuery.data.length === 0 && debouncedQuery.length >= 2 && (
              <Text style={styles.searchEmpty}>
                No matches. Try a nearby estate or street name.
              </Text>
            )}

            {searchQuery.data?.map((candidate) => (
              <TouchableOpacity
                key={`${candidate.localityId}:${candidate.placeLabel}`}
                style={styles.candidateRow}
                onPress={() => handleAdd(candidate)}
                disabled={updateMutation.isPending}
                accessible
                accessibilityRole="button"
                accessibilityLabel={`Follow ${candidate.placeLabel}`}
              >
                <View style={styles.candidateText}>
                  <Text style={styles.candidatePrimary} numberOfLines={1}>
                    {candidate.placeLabel}
                  </Text>
                  <Text style={styles.candidateSecondary} numberOfLines={1}>
                    {candidate.wardName}
                    {candidate.cityName ? ` · ${candidate.cityName}` : ''}
                  </Text>
                </View>
                <Ionicons name="add-circle" size={22} color="#1a3a2f" />
              </TouchableOpacity>
            ))}
          </>
        )}

        {toast !== null && (
          <Text
            style={styles.toast}
            accessible
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
          >
            {toast}
          </Text>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

// ─── Local error type for the mutation ────────────────────────────────────

class ApiResultError extends Error {
  constructor(public readonly apiError: ApiError) {
    super(apiError.message);
    this.name = 'ApiResultError';
  }
}

// ─── Styles ───────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#FFFFFF' },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#E5E7EB',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#111827' },
  navSpacer: { width: 24 },
  content: { padding: 20, gap: 14 },
  badge: {
    backgroundColor: '#F0FDF4',
    borderRadius: 20,
    paddingHorizontal: 12,
    paddingVertical: 6,
    alignSelf: 'flex-start',
  },
  badgeAtCapacity: { backgroundColor: '#FEF2F2' },
  badgeText: { fontSize: 13, fontWeight: '600', color: '#1a3a2f' },
  badgeTextAtCapacity: { color: '#991B1B' },
  empty: { fontSize: 14, color: '#6B7280', lineHeight: 20 },
  sectionLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: '#374151',
    marginTop: 8,
  },
  wardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
    borderRadius: 10,
    padding: 14,
    gap: 10,
  },
  wardRowText: { flex: 1 },
  wardName: { fontSize: 15, color: '#111827', fontWeight: '600' },
  wardCity: { fontSize: 12, color: '#6B7280', marginTop: 2 },
  searchInput: {
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 14,
    color: '#111827',
    backgroundColor: '#FFFFFF',
  },
  searchEmpty: { fontSize: 13, color: '#6B7280', marginTop: 4 },
  candidateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#E5E7EB',
    borderRadius: 10,
    padding: 12,
    gap: 10,
  },
  candidateText: { flex: 1 },
  candidatePrimary: { fontSize: 14, color: '#111827', fontWeight: '500' },
  candidateSecondary: { fontSize: 12, color: '#6B7280', marginTop: 2 },
  capacityHint: {
    fontSize: 13,
    color: '#991B1B',
    backgroundColor: '#FEF2F2',
    borderRadius: 8,
    padding: 12,
  },
  toast: {
    fontSize: 14,
    color: '#991B1B',
    backgroundColor: '#FEF2F2',
    borderRadius: 8,
    padding: 12,
  },
});
