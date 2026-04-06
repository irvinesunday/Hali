// apps/citizen-mobile/app/(app)/settings/wards.tsx
//
// Ward following — view, add, remove. Max 5 enforced both client-side
// (button disabled at capacity) and server-side (422
// max_followed_localities_exceeded).
//
// PUT /v1/localities/followed replaces the full set, so add and remove
// both send the entire updated array in one call.

import React, { useMemo, useState } from 'react';
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
  setFollowedLocalities,
} from '../../../src/api/localities';
import { useLocalityContext } from '../../../src/context/LocalityContext';
import { Loading } from '../../../src/components/common/Loading';
import { MAX_FOLLOWED_WARDS } from '../../../src/config/constants';
import type { ApiError } from '../../../src/types/api';

export default function WardsSettingsScreen(): React.ReactElement {
  const router = useRouter();
  const qc = useQueryClient();
  const {
    activeLocalityId,
    setFollowedLocalityIds,
    setActiveLocalityId,
  } = useLocalityContext();

  const [newWardId, setNewWardId] = useState('');
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

  const currentIds = useMemo<string[]>(
    () => followedQuery.data?.localityIds ?? [],
    [followedQuery.data],
  );

  // ── Update mutation ───────────────────────────────────────────────────────
  // setFollowedLocalities returns Result<void, ApiError>; we throw inside
  // mutationFn so React Query's onError fires with a typed Error.
  const updateMutation = useMutation<void, Error, string[]>({
    mutationFn: async (ids) => {
      const result = await setFollowedLocalities({ localityIds: ids });
      if (!result.ok) throw new ApiResultError(result.error);
    },
    onSuccess: (_void, ids) => {
      // Mirror into LocalityContext immediately so the home feed picks up
      // the new follows without waiting for refetch.
      setFollowedLocalityIds(ids);

      // Smart active-ward selection: keep the current active if it's still
      // in the set, otherwise fall back to the first ward (or null if empty).
      // This avoids the bug where add-then-set unconditionally jumped the
      // active ward to ids[0] even when the user was deliberately on a
      // different one.
      if (activeLocalityId === null || !ids.includes(activeLocalityId)) {
        setActiveLocalityId(ids[0] ?? null);
      }

      qc.invalidateQueries({ queryKey: ['localities', 'followed'] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
    onError: (err) => {
      // 422 max_followed_localities_exceeded is the most likely failure
      // we can still recover from — show a specific toast.
      if (err instanceof ApiResultError) {
        if (err.apiError.code === 'max_followed_localities_exceeded') {
          setToast(`You can follow at most ${MAX_FOLLOWED_WARDS} wards.`);
          return;
        }
        setToast(err.apiError.message);
        return;
      }
      setToast('Could not update followed wards. Please try again.');
    },
  });

  function handleAdd(): void {
    const trimmed = newWardId.trim();
    if (trimmed === '') return;
    if (currentIds.includes(trimmed)) {
      setToast('You are already following this ward.');
      return;
    }
    if (currentIds.length >= MAX_FOLLOWED_WARDS) {
      setToast(`You can follow at most ${MAX_FOLLOWED_WARDS} wards.`);
      return;
    }
    setToast(null);
    updateMutation.mutate([...currentIds, trimmed]);
    setNewWardId('');
  }

  function handleRemove(id: string): void {
    setToast(null);
    updateMutation.mutate(currentIds.filter((w) => w !== id));
  }

  if (followedQuery.isLoading && !followedQuery.data) {
    return <Loading />;
  }

  const atCapacity = currentIds.length >= MAX_FOLLOWED_WARDS;
  const canAdd =
    newWardId.trim().length > 0 && !atCapacity && !updateMutation.isPending;

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

      <ScrollView contentContainerStyle={styles.content}>
        <View
          style={[styles.badge, atCapacity && styles.badgeAtCapacity]}
          accessible
          accessibilityRole="text"
          accessibilityLabel={`${currentIds.length} of ${MAX_FOLLOWED_WARDS} wards followed`}
        >
          <Text
            style={[
              styles.badgeText,
              atCapacity && styles.badgeTextAtCapacity,
            ]}
          >
            {currentIds.length} of {MAX_FOLLOWED_WARDS} wards followed
          </Text>
        </View>

        {currentIds.length === 0 && (
          <Text style={styles.empty}>
            You&apos;re not following any wards yet. Add a ward ID below to
            start seeing local civic signals.
          </Text>
        )}

        {currentIds.map((id) => (
          <View key={id} style={styles.wardRow}>
            <Text style={styles.wardId} numberOfLines={1}>
              {id}
            </Text>
            <TouchableOpacity
              onPress={() => handleRemove(id)}
              hitSlop={8}
              disabled={updateMutation.isPending}
              accessible
              accessibilityRole="button"
              accessibilityLabel={`Remove ward ${id.slice(0, 8)}`}
            >
              <Ionicons name="close-circle" size={22} color="#DC2626" />
            </TouchableOpacity>
          </View>
        ))}

        <View style={styles.addRow}>
          <TextInput
            style={styles.addInput}
            value={newWardId}
            onChangeText={setNewWardId}
            placeholder="Ward / locality ID (UUID)"
            placeholderTextColor="#9CA3AF"
            autoCapitalize="none"
            autoCorrect={false}
            editable={!atCapacity && !updateMutation.isPending}
            accessible
            accessibilityLabel="Ward ID input"
          />
          <TouchableOpacity
            style={[styles.addBtn, !canAdd && styles.addBtnDisabled]}
            onPress={handleAdd}
            disabled={!canAdd}
            accessible
            accessibilityRole="button"
            accessibilityLabel="Add ward"
          >
            {updateMutation.isPending ? (
              <ActivityIndicator color="#FFFFFF" size="small" />
            ) : (
              <Text style={styles.addBtnText}>Add</Text>
            )}
          </TouchableOpacity>
        </View>

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

        <Text style={styles.hint}>
          Enter the locality ID (UUID) of the ward you want to follow.
          Maximum {MAX_FOLLOWED_WARDS} wards. The first ward you add becomes
          your active ward in the home feed.
        </Text>
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
  wardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F9FAFB',
    borderRadius: 10,
    padding: 14,
    gap: 10,
  },
  wardId: {
    flex: 1,
    fontSize: 14,
    color: '#111827',
    fontFamily: 'monospace',
  },
  addRow: { flexDirection: 'row', gap: 10, alignItems: 'flex-start' },
  addInput: {
    flex: 1,
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 14,
    color: '#111827',
    backgroundColor: '#FFFFFF',
  },
  addBtn: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 12,
    paddingHorizontal: 18,
    minWidth: 70,
    alignItems: 'center',
    justifyContent: 'center',
  },
  addBtnDisabled: { backgroundColor: '#9CA3AF' },
  addBtnText: { color: '#FFFFFF', fontSize: 14, fontWeight: '600' },
  toast: {
    fontSize: 14,
    color: '#991B1B',
    backgroundColor: '#FEF2F2',
    borderRadius: 8,
    padding: 12,
  },
  hint: { fontSize: 13, color: '#9CA3AF', lineHeight: 18 },
});
