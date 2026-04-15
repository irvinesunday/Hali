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
import { ArrowLeft, XCircle, PlusCircle, Navigation } from 'lucide-react-native';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as Location from 'expo-location';
import {
  getFollowedLocalities,
  resolveByCoordinates,
  searchLocalities,
  setFollowedLocalities,
} from '../../../src/api/localities';
import { useLocalityContext } from '../../../src/context/LocalityContext';
import { Loading } from '../../../src/components/common/Loading';
import {
  FEATURE_GPS_LOCALITY_OPT_IN,
  MAX_FOLLOWED_WARDS,
} from '../../../src/config/constants';
import { mapWardsUpdateErrorToToast } from '../../../src/utils/wardsUpdateErrorMessage';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../../src/theme';
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
  const [gpsLoading, setGpsLoading] = useState(false);

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
        setToast(
          mapWardsUpdateErrorToToast(err.apiError, {
            maxFollowedWards: MAX_FOLLOWED_WARDS,
          }),
        );
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

  async function handleUseLocation(): Promise<void> {
    if (atCapacity) return;

    setGpsLoading(true);
    setToast(null);

    try {
      const { status } = await Location.requestForegroundPermissionsAsync();

      if (status !== Location.PermissionStatus.GRANTED) {
        setToast('Location permission denied. Search manually.');
        return;
      }

      const position = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.Balanced,
      });

      const result = await resolveByCoordinates(
        position.coords.latitude,
        position.coords.longitude,
      );

      if (!result.ok) {
        if (result.error.status === 404) {
          setToast('No ward found at your location. Try searching manually.');
        } else {
          setToast('Could not resolve your location. Try searching manually.');
        }
        return;
      }

      const asSearchResult: LocalitySearchResult = {
        localityId: result.value.localityId,
        placeLabel: result.value.cityName
          ? `${result.value.wardName}, ${result.value.cityName}`
          : result.value.wardName,
        wardName: result.value.wardName,
        cityName: result.value.cityName,
      };

      handleAdd(asSearchResult);
    } catch {
      setToast('Could not access location. Please try searching manually.');
    } finally {
      setGpsLoading(false);
    }
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
          <ArrowLeft size={24} color={Colors.foreground} />
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
              <XCircle size={22} color={Colors.destructive} />
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
              placeholderTextColor={Colors.faintForeground}
              autoCapitalize="words"
              autoCorrect={false}
              editable={!updateMutation.isPending}
              accessible
              accessibilityLabel="Area search"
            />

            {/* GPS opt-in — gated by feature flag until backend ships.
                Backend endpoint is currently a stub returning 404. */}
            {FEATURE_GPS_LOCALITY_OPT_IN && (
            <TouchableOpacity
              style={[styles.gpsButton, gpsLoading && styles.gpsButtonLoading]}
              onPress={() => void handleUseLocation()}
              disabled={gpsLoading || updateMutation.isPending}
              accessible
              accessibilityRole="button"
              accessibilityLabel="Use my current location"
            >
              {gpsLoading ? (
                <ActivityIndicator size="small" color={Colors.primary} />
              ) : (
                <Navigation size={16} color={Colors.primary} strokeWidth={2} />
              )}
              <Text style={styles.gpsButtonText}>
                {gpsLoading ? 'Finding your location…' : 'Use my current location'}
              </Text>
            </TouchableOpacity>
            )}

            {searchQuery.isFetching && (
              <ActivityIndicator color={Colors.primary} style={{ marginTop: Spacing.sm }} />
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
                <PlusCircle size={22} color={Colors.primary} />
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
  safe: { flex: 1, backgroundColor: Colors.card },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  navTitle: {
    fontSize: FontSize.cardTitle,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  navSpacer: { width: 24 },
  content: { padding: Spacing.xl, gap: Spacing.md },
  badge: {
    backgroundColor: Colors.emeraldSubtle,
    borderRadius: Radius.full,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    alignSelf: 'flex-start',
  },
  badgeAtCapacity: { backgroundColor: Colors.destructiveSubtle },
  badgeText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.semiBold,
    color: Colors.primary,
  },
  badgeTextAtCapacity: { color: Colors.destructive },
  empty: { fontSize: FontSize.body, color: Colors.mutedForeground, lineHeight: 20 },
  sectionLabel: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
    marginTop: Spacing.sm,
  },
  wardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.muted,
    borderRadius: Radius.md,
    padding: Spacing.md,
    gap: Spacing.sm,
  },
  wardRowText: { flex: 1 },
  wardName: {
    fontSize: FontSize.cardTitle,
    color: Colors.foreground,
    fontFamily: FontFamily.semiBold,
  },
  wardCity: {
    fontSize: FontSize.badge,
    color: Colors.mutedForeground,
    marginTop: 2,
  },
  searchInput: {
    borderWidth: 1.5,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.md,
    fontSize: FontSize.body,
    color: Colors.foreground,
    backgroundColor: Colors.card,
  },
  searchEmpty: {
    fontSize: FontSize.bodySmall,
    color: Colors.mutedForeground,
    marginTop: Spacing.xs,
  },
  candidateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.card,
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    padding: Spacing.md,
    gap: Spacing.sm,
  },
  candidateText: { flex: 1 },
  candidatePrimary: {
    fontSize: FontSize.body,
    color: Colors.foreground,
    fontFamily: FontFamily.medium,
  },
  candidateSecondary: {
    fontSize: FontSize.badge,
    color: Colors.mutedForeground,
    marginTop: 2,
  },
  capacityHint: {
    fontSize: FontSize.bodySmall,
    color: Colors.destructive,
    backgroundColor: Colors.destructiveSubtle,
    borderRadius: Radius.sm,
    padding: Spacing.md,
  },
  gpsButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    paddingVertical: Spacing.sm,
    paddingHorizontal: Spacing.md,
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: Colors.primary + '40',
    backgroundColor: Colors.primarySubtle,
  },
  gpsButtonLoading: {
    opacity: 0.6,
  },
  gpsButtonText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.primary,
  },
  toast: {
    fontSize: FontSize.body,
    color: Colors.destructive,
    backgroundColor: Colors.destructiveSubtle,
    borderRadius: Radius.sm,
    padding: Spacing.md,
  },
});
