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
// Guest mode: unauthenticated users see the full feed read-only.
// Contribution gates are handled per-action downstream.

import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  RefreshControl,
  TouchableOpacity,
  StyleSheet,
  TextInput,
  FlatList,
  ActivityIndicator,
  Keyboard,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { MapPin, ChevronDown, Search, Navigation, X, Check } from 'lucide-react-native';
import { useQuery } from '@tanstack/react-query';

import { ClusterCard } from '../../src/components/feed/ClusterCard';
import { Loading } from '../../src/components/common/Loading';
import {
  SectionHeader,
  CalmState,
  FAB,
  OfficialUpdateRow,
  FeedbackButton,
} from '../../src/components/shared';

import { getAllLocalities, getFollowedLocalities } from '../../src/api/localities';
import { useHome, ApiResultError } from '../../src/hooks/useClusters';
import { useLocalityContext } from '../../src/context/LocalityContext';
import { useAuth } from '../../src/context/AuthContext';
import { filterLocalities } from '../../src/utils/localityFilter';
import { formatRelativeTime, getCategoryInstitutionName } from '../../src/utils/formatters';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
  Shadows,
  ScreenPaddingH,
  ScreenPaddingBottom,
} from '../../src/theme';
import type {
  ClusterResponse,
  OfficialPostResponse,
  PagedSection,
  LocalitySummary,
} from '../../src/types/api';

// ─── Helpers ────────────────────────────────────────────────────────────────

function isSectionEmpty<T>(section: PagedSection<T> | undefined): boolean {
  return !section || section.items.length === 0;
}

// ─── Component ───────────────────────────────────────────────────────────────

export default function HomeScreen(): React.ReactElement {
  const router = useRouter();
  const { authState } = useAuth();
  const isAuthenticated = authState.status === 'authenticated';
  const {
    activeLocality,
    followedLocalities,
    setActiveLocalityId,
    setActiveLocality,
    setFollowedLocalities,
  } = useLocalityContext();

  const [localitySelectorOpen, setLocalitySelectorOpen] = useState(false);

  // Guard against stacking multiple auth screens if the FAB is tapped
  // repeatedly while unauthenticated. Mirrors the navigatingToAuthRef
  // pattern in cluster detail.
  const navigatingToAuthRef = useRef(false);
  useEffect(() => {
    if (isAuthenticated) {
      navigatingToAuthRef.current = false;
    }
  }, [isAuthenticated]);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  // ── Load followed localities ──────────────────────────────────────────────
  // Guests have no follows; calling /v1/localities/followed without tokens
  // would trigger the API client's 401 refresh flow and leave followsLoaded
  // false, blanking the feed. Skip the request entirely and treat guests as
  // a loaded-empty follows list so the NoFollowsState renders correctly.
  const localitiesQuery = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: async () => {
      const result = await getFollowedLocalities();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
    staleTime: 60_000,
    retry: 1,
    enabled: isAuthenticated,
  });

  useEffect(() => {
    if (!isAuthenticated) {
      setFollowedLocalities([]);
      return;
    }
    if (localitiesQuery.data) {
      setFollowedLocalities(localitiesQuery.data);
    }
  }, [isAuthenticated, localitiesQuery.data, setFollowedLocalities]);

  // ── Home feed ─────────────────────────────────────────────────────────────
  const homeQuery = useHome(activeLocality?.localityId);

  useEffect(() => {
    if (homeQuery.dataUpdatedAt > 0) {
      setLastUpdatedAt(new Date(homeQuery.dataUpdatedAt));
    }
  }, [homeQuery.dataUpdatedAt]);

  // ── Derived state ─────────────────────────────────────────────────────────
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

  // Derive readiness locally from React Query state so that a guest→auth
  // transition doesn't flash NoFollowsState while the authed follows query
  // is still in flight. `followsLoaded` from context is permanently true
  // once set (including by the guest branch), so it cannot be relied on
  // after auth state changes without a full remount of LocalityProvider.
  const followsReady =
    !isAuthenticated ||
    localitiesQuery.isSuccess ||
    localitiesQuery.isError;

  const hasNoFollows = followsReady && followedLocalities.length === 0;

  const localityDisplayName =
    activeLocality?.displayLabel ??
    activeLocality?.wardName ??
    'Select an area';

  const localityStateText = (() => {
    if (!followsReady) return '';
    if (hasNoFollows) return 'Follow a ward to see activity';
    if (isCalmState) return 'Currently calm';
    const count = (feed?.activeNow.items.length ?? 0) +
                  (feed?.otherActiveSignals.items.length ?? 0);
    if (count === 0) return 'No active signals';
    if (count === 1) return '1 active signal';
    return `${count} active signals`;
  })();

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <SafeAreaView style={styles.safe} edges={['top']}>

      {/* ── Header ───────────────────────────────────────────────────── */}
      <View style={styles.header}>
        <Text style={styles.wordmark}>Hali</Text>
        <TouchableOpacity
          style={styles.localitySelector}
          onPress={() => setLocalitySelectorOpen(true)}
          accessibilityRole="button"
          accessibilityLabel="Change area"
        >
          <MapPin size={14} color={Colors.primary} strokeWidth={2} />
          <Text style={styles.localityName} numberOfLines={1}>
            {localityDisplayName}
          </Text>
          <ChevronDown size={14} color={Colors.mutedForeground} strokeWidth={2} />
        </TouchableOpacity>
        {localityStateText !== '' && (
          <Text style={styles.localityStateText}>{localityStateText}</Text>
        )}
      </View>

      {/* ── Feed body ────────────────────────────────────────────────── */}
      {homeQuery.isLoading && !feed ? (
        <Loading />
      ) : homeQuery.isError ? (
        <ErrorState
          message={(homeQuery.error as ApiResultError).message}
          onRetry={() => void homeQuery.refetch()}
        />
      ) : (
        <ScrollView
          style={styles.flex}
          contentContainerStyle={styles.scrollContent}
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={homeQuery.isFetching && !homeQuery.isLoading}
              onRefresh={() => void homeQuery.refetch()}
              tintColor={Colors.primary}
            />
          }
        >
          {hasNoFollows ? (
            <NoFollowsState
              isAuthenticated={isAuthenticated}
              onOpenPicker={() => setLocalitySelectorOpen(true)}
            />
          ) : isCalmState ? (
            <CalmState
              localityName={localityDisplayName}
              lastCheckedText={
                lastUpdatedAt
                  ? `Last checked ${formatRelativeTime(lastUpdatedAt.toISOString())}`
                  : undefined
              }
            />
          ) : (
            <>
              <FeedSection
                title="Active Now"
                clusters={feed?.activeNow.items ?? []}
              />
              <OfficialUpdatesSection
                posts={feed?.officialUpdates.items ?? []}
              />
              <FeedSection
                title="Recurring at This Time"
                clusters={feed?.recurringAtThisTime.items ?? []}
              />
              <FeedSection
                title="Other Active Signals"
                clusters={feed?.otherActiveSignals.items ?? []}
              />
              {lastUpdatedAt !== null && (
                <Text style={styles.lastUpdated}>
                  Updated {formatRelativeTime(lastUpdatedAt.toISOString())}
                </Text>
              )}
            </>
          )}
        </ScrollView>
      )}

      {/* ── Persistent FAB ──────────────────────────────────────────── */}
      <FAB onPress={() => {
        if (!isAuthenticated) {
          if (navigatingToAuthRef.current) return;
          navigatingToAuthRef.current = true;
          router.push('/(auth)/phone');
          return;
        }
        router.push('/(app)/compose/text');
      }} />

      {/* ── Feedback button ─────────────────────────────────────────── */}
      <FeedbackButton screen="home" />

      {/* ── Locality selector sheet ──────────────────────────────────── */}
      {localitySelectorOpen && (
        <LocalitySelectorSheet
          onClose={() => setLocalitySelectorOpen(false)}
          followedLocalities={followedLocalities}
          activeLocalityId={activeLocality?.localityId ?? null}
          isAuthenticated={isAuthenticated}
          onSelectWard={(ward) => {
            // If the ward is one of the user's existing follows, route
            // through setActiveLocalityId so the stored displayLabel is
            // preserved. Otherwise drop the bare ward into activeLocality
            // (no follow side-effect — works for both guests and authed
            // users who are browsing).
            const followed = followedLocalities.find(
              (l) => l.localityId === ward.localityId,
            );
            if (followed !== undefined) {
              setActiveLocalityId(ward.localityId);
            } else {
              setActiveLocality({
                localityId: ward.localityId,
                wardName: ward.wardName,
                displayLabel: null,
                cityName: ward.cityName,
              });
            }
            setLocalitySelectorOpen(false);
          }}
        />
      )}
    </SafeAreaView>
  );
}

// ─── Feed sections ──────────────────────────────────────────────────────────

function FeedSection({
  title,
  clusters,
}: {
  title: string;
  clusters: ClusterResponse[];
}): React.ReactElement | null {
  if (clusters.length === 0) return null;
  return (
    <View style={styles.section}>
      <SectionHeader label={title} />
      <View style={styles.cardList}>
        {clusters.map((c) => (
          <ClusterCard key={c.id} cluster={c} />
        ))}
      </View>
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
      <SectionHeader label="Official Updates" />
      <View style={styles.officialList}>
        {posts.map((post) => (
          <OfficialUpdateRow
            key={post.id}
            institutionName={getCategoryInstitutionName(post.category)}
            message={post.title}
          />
        ))}
      </View>
    </View>
  );
}

// ─── Empty / error states ───────────────────────────────────────────────────

function NoFollowsState({
  isAuthenticated,
  onOpenPicker,
}: {
  isAuthenticated: boolean;
  onOpenPicker: () => void;
}): React.ReactElement {
  return (
    <View style={styles.emptyContainer}>
      <MapPin size={32} color={Colors.mutedForeground} strokeWidth={1.5} />
      <Text style={styles.emptyTitle}>
        {isAuthenticated ? 'Follow a ward to see activity' : 'Choose an area to explore'}
      </Text>
      <Text style={styles.emptyBody}>
        {isAuthenticated
          ? 'Hali shows you civic signals in the wards you follow. Pick up to 5.'
          : 'Search for a ward to browse civic signals in that area.'}
      </Text>
      <TouchableOpacity
        style={styles.primaryCta}
        onPress={onOpenPicker}
        accessibilityRole="button"
      >
        <Text style={styles.primaryCtaText}>Choose an area</Text>
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
    <View style={styles.emptyContainer}>
      <Text style={styles.emptyTitle}>Couldn't load the feed</Text>
      <Text style={styles.emptyBody}>{message}</Text>
      <TouchableOpacity
        style={styles.primaryCta}
        onPress={onRetry}
        accessibilityRole="button"
      >
        <Text style={styles.primaryCtaText}>Try again</Text>
      </TouchableOpacity>
    </View>
  );
}

// ─── Locality selector sheet ────────────────────────────────────────────────
//
// Searchable ward picker. Fetches the canonical ward list once
// (GET /v1/localities/wards, cached 24h server-side and staleTime:Infinity
// client-side) and filters it locally so typing is instant. Shows the
// user's followed wards at the top for fast switching, then the full
// "Browse all wards" list below.

interface LocalitySelectorSheetProps {
  onClose: () => void;
  followedLocalities: Array<{ localityId: string; wardName: string; displayLabel: string | null }>;
  activeLocalityId: string | null;
  isAuthenticated: boolean;
  /** Tap on any ward row — follow-state handled by the caller. */
  onSelectWard: (ward: LocalitySummary) => void;
}

type SheetRow =
  | { kind: 'header'; id: string; label: string }
  | { kind: 'ward'; ward: LocalitySummary };

function LocalitySelectorSheet({
  onClose,
  followedLocalities,
  activeLocalityId,
  isAuthenticated,
  onSelectWard,
}: LocalitySelectorSheetProps): React.ReactElement {
  const [searchQuery, setSearchQuery] = useState('');

  // Full ward list — static-ish data, cached aggressively. The query only
  // mounts when the sheet is open (the parent conditionally renders this
  // component) so there's no cost when the picker is closed.
  const wardsQuery = useQuery({
    queryKey: ['localities', 'all'],
    queryFn: async () => {
      const result = await getAllLocalities();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
    staleTime: 24 * 60 * 60 * 1000, // 24h — backend cache matches
    retry: 1,
  });

  const allWards = wardsQuery.data ?? [];

  const filteredWards = useMemo<LocalitySummary[]>(
    () => filterLocalities(allWards, searchQuery),
    [allWards, searchQuery],
  );

  const listData = useMemo<SheetRow[]>(() => {
    const rows: SheetRow[] = [];
    const followedIds = new Set(followedLocalities.map((l) => l.localityId));
    const trimmedQuery = searchQuery.trim();

    // Section 1: Followed wards. Only show this section when the user
    // is browsing (no search query) — once they start typing, folding
    // followed + all wards into one filtered list is clearer.
    if (trimmedQuery.length === 0 && followedLocalities.length > 0) {
      rows.push({ kind: 'header', id: 'h:followed', label: 'Your areas' });
      for (const fl of followedLocalities) {
        rows.push({
          kind: 'ward',
          ward: {
            localityId: fl.localityId,
            wardName: fl.displayLabel ?? fl.wardName,
            cityName: null,
          },
        });
      }
    }

    // Section 2: Browse / search results over the full ward list,
    // excluding any ward already shown in the followed section above.
    const browseWards =
      trimmedQuery.length === 0
        ? filteredWards.filter((w) => !followedIds.has(w.localityId))
        : filteredWards;

    if (browseWards.length > 0) {
      rows.push({
        kind: 'header',
        id: 'h:browse',
        label:
          trimmedQuery.length === 0
            ? followedLocalities.length > 0
              ? 'Browse all wards'
              : 'All wards'
            : 'Matches',
      });
      for (const w of browseWards) {
        rows.push({ kind: 'ward', ward: w });
      }
    }

    return rows;
  }, [followedLocalities, filteredWards, searchQuery]);

  const showEmptyState =
    wardsQuery.isSuccess &&
    searchQuery.trim().length > 0 &&
    filteredWards.length === 0;

  const showLoadError = wardsQuery.isError;
  const showInitialLoading = wardsQuery.isLoading && allWards.length === 0;

  return (
    <View style={styles.sheetOverlay}>
      <TouchableOpacity
        style={styles.sheetBackdrop}
        activeOpacity={1}
        onPress={() => { Keyboard.dismiss(); onClose(); }}
      />
      <View style={styles.sheet}>
        {/* Sheet header */}
        <View style={styles.sheetHeader}>
          <Text style={styles.sheetTitle}>
            {isAuthenticated ? 'Your areas' : 'Browse an area'}
          </Text>
          <TouchableOpacity
            onPress={onClose}
            accessibilityLabel="Close"
            accessibilityRole="button"
          >
            <X size={20} color={Colors.mutedForeground} />
          </TouchableOpacity>
        </View>

        {/* Search input */}
        <View style={styles.searchRow}>
          <Search size={16} color={Colors.mutedForeground} strokeWidth={2} />
          <TextInput
            style={styles.searchInput}
            placeholder="Search wards by name…"
            placeholderTextColor={Colors.faintForeground}
            value={searchQuery}
            onChangeText={setSearchQuery}
            autoCorrect={false}
            autoCapitalize="none"
            returnKeyType="search"
            accessibilityLabel="Search wards"
          />
          {showInitialLoading && (
            <ActivityIndicator size="small" color={Colors.primary} />
          )}
        </View>

        {/* GPS opt-in */}
        <TouchableOpacity
          style={styles.gpsRow}
          onPress={() => {
            // GPS opt-in — navigates to wards settings where
            // ILocalityService.ResolveByCoordinatesAsync is wired.
            // Full GPS resolution implemented in Phase G (settings screen).
            onClose();
          }}
          accessibilityRole="button"
          accessibilityLabel="Use my current location"
        >
          <Navigation size={14} color={Colors.primary} strokeWidth={2} />
          <Text style={styles.gpsText}>Use my current location</Text>
        </TouchableOpacity>

        {/* Results list */}
        <FlatList
          data={listData}
          keyExtractor={(item) =>
            item.kind === 'header' ? item.id : `w:${item.ward.localityId}`
          }
          renderItem={({ item }) => {
            if (item.kind === 'header') {
              return (
                <Text style={styles.sheetSectionHeader}>{item.label}</Text>
              );
            }

            const ward = item.ward;
            const isActive = ward.localityId === activeLocalityId;
            const label = ward.cityName
              ? `${ward.wardName}, ${ward.cityName}`
              : ward.wardName;

            return (
              <TouchableOpacity
                style={[
                  styles.localityRow,
                  isActive && styles.localityRowActive,
                ]}
                onPress={() => {
                  Keyboard.dismiss();
                  onSelectWard(ward);
                }}
                accessibilityRole="button"
                accessibilityState={{ selected: isActive }}
                accessibilityLabel={
                  isActive ? `${label}, currently selected` : label
                }
              >
                <Text
                  style={[
                    styles.localityRowText,
                    isActive && styles.localityRowTextActive,
                  ]}
                >
                  {label}
                </Text>
                {isActive && (
                  <Check size={16} color={Colors.primary} strokeWidth={2.5} />
                )}
              </TouchableOpacity>
            );
          }}
          ListEmptyComponent={
            showLoadError ? (
              <Text style={styles.searchEmpty}>
                Couldn't load wards. Pull down to retry.
              </Text>
            ) : showEmptyState ? (
              <Text style={styles.searchEmpty}>No wards match "{searchQuery.trim()}"</Text>
            ) : showInitialLoading ? (
              <Text style={styles.searchEmpty}>Loading wards…</Text>
            ) : null
          }
          keyboardShouldPersistTaps="handled"
          style={styles.sheetList}
        />
      </View>
    </View>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: {
    flex: 1,
    backgroundColor: Colors.background,
  },
  flex: { flex: 1 },

  // Header
  header: {
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing.md,
    paddingBottom: Spacing.sm,
    backgroundColor: Colors.background,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border + '80',
  },
  wordmark: {
    fontSize: FontSize.appName,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
    letterSpacing: -0.5,
  },
  localitySelector: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
    marginTop: Spacing.xs + 2,
  },
  localityName: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.foreground,
    flexShrink: 1,
  },
  localityStateText: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    marginTop: 2,
    marginLeft: 18, // aligns under locality name (icon width + gap)
  },

  // Feed
  scrollContent: {
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing.lg,
    paddingBottom: ScreenPaddingBottom,
    gap: Spacing['2xl'],
  },
  section: { gap: Spacing.md },
  cardList: { gap: Spacing.md },
  officialList: { gap: Spacing.sm },
  lastUpdated: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
    textAlign: 'center',
    marginTop: Spacing.sm,
  },

  // Empty / error states
  emptyContainer: {
    paddingVertical: Spacing['4xl'],
    paddingHorizontal: Spacing.lg,
    gap: Spacing.md,
    alignItems: 'flex-start',
  },
  emptyTitle: {
    fontSize: FontSize.title,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  emptyBody: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    lineHeight: FontSize.body * 1.5,
  },
  primaryCta: {
    marginTop: Spacing.sm,
    backgroundColor: Colors.primary,
    paddingVertical: Spacing.md,
    paddingHorizontal: Spacing.xl,
    borderRadius: Radius.md,
  },
  primaryCtaText: {
    color: Colors.primaryForeground,
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
  },

  // Locality selector sheet
  sheetOverlay: {
    ...StyleSheet.absoluteFillObject,
    zIndex: 50,
    justifyContent: 'flex-end',
  },
  sheetBackdrop: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: 'rgba(0,0,0,0.4)',
  },
  sheet: {
    backgroundColor: Colors.card,
    borderTopLeftRadius: Radius['2xl'],
    borderTopRightRadius: Radius['2xl'],
    paddingTop: Spacing.lg,
    paddingBottom: Spacing['3xl'],
    maxHeight: '70%',
    ...Shadows.modal,
  },
  sheetHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: ScreenPaddingH,
    marginBottom: Spacing.md,
  },
  sheetTitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  searchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    marginHorizontal: ScreenPaddingH,
    backgroundColor: Colors.muted,
    borderRadius: Radius.md,
    borderWidth: 1,
    borderColor: Colors.border,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm + 2,
    marginBottom: Spacing.sm,
  },
  searchInput: {
    flex: 1,
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
    padding: 0,
  },
  gpsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    paddingHorizontal: ScreenPaddingH,
    paddingVertical: Spacing.sm + 2,
    marginBottom: Spacing.xs,
  },
  gpsText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.primary,
  },
  sheetList: {
    maxHeight: 360,
  },
  sheetSectionHeader: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.semiBold,
    color: Colors.mutedForeground,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing.md,
    paddingBottom: Spacing.xs,
  },
  localityRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: Spacing.md,
    paddingHorizontal: ScreenPaddingH,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border + '60',
  },
  localityRowActive: {
    backgroundColor: Colors.primarySubtle,
  },
  localityRowText: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
  },
  localityRowTextActive: {
    fontFamily: FontFamily.medium,
    color: Colors.primary,
  },
  searchEmpty: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    paddingHorizontal: ScreenPaddingH,
    paddingVertical: Spacing.md,
  },
});
