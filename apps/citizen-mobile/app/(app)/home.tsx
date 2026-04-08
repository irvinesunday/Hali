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

import { getFollowedLocalities, searchLocalities } from '../../src/api/localities';
import { useHome, ApiResultError } from '../../src/hooks/useClusters';
import { useLocalityContext } from '../../src/context/LocalityContext';
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
  LocalitySearchResult,
} from '../../src/types/api';

// ─── Helpers ────────────────────────────────────────────────────────────────

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

  const [localitySelectorOpen, setLocalitySelectorOpen] = useState(false);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  // ── Load followed localities ──────────────────────────────────────────────
  const localitiesQuery = useQuery({
    queryKey: ['localities', 'followed'],
    queryFn: async () => {
      const result = await getFollowedLocalities();
      if (!result.ok) throw new Error(result.error.message);
      return result.value;
    },
    staleTime: 60_000,
    retry: 1,
  });

  useEffect(() => {
    if (localitiesQuery.data) {
      setFollowedLocalities(localitiesQuery.data);
    }
  }, [localitiesQuery.data, setFollowedLocalities]);

  // ── Home feed ─────────────────────────────────────────────────────────────
  const homeQuery = useHome();

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

  const hasNoFollows = followsLoaded && followedLocalities.length === 0;

  const localityDisplayName =
    activeLocality?.displayLabel ??
    activeLocality?.wardName ??
    'Select an area';

  const localityStateText = (() => {
    if (!followsLoaded) return '';
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
      <FAB onPress={() => router.push('/(app)/compose/text')} />

      {/* ── Feedback button ─────────────────────────────────────────── */}
      <FeedbackButton screen="home" />

      {/* ── Locality selector sheet ──────────────────────────────────── */}
      {localitySelectorOpen && (
        <LocalitySelectorSheet
          onClose={() => setLocalitySelectorOpen(false)}
          followedLocalities={followedLocalities}
          activeLocalityId={activeLocality?.localityId ?? null}
          onSelectLocality={(id) => {
            setActiveLocalityId(id);
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
  onOpenPicker,
}: {
  onOpenPicker: () => void;
}): React.ReactElement {
  return (
    <View style={styles.emptyContainer}>
      <MapPin size={32} color={Colors.mutedForeground} strokeWidth={1.5} />
      <Text style={styles.emptyTitle}>Follow a ward to see activity</Text>
      <Text style={styles.emptyBody}>
        Hali shows you civic signals in the wards you follow. Pick up to 5.
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

interface LocalitySelectorSheetProps {
  onClose: () => void;
  followedLocalities: Array<{ localityId: string; wardName: string; displayLabel: string | null }>;
  activeLocalityId: string | null;
  onSelectLocality: (id: string) => void;
}

function LocalitySelectorSheet({
  onClose,
  followedLocalities,
  activeLocalityId,
  onSelectLocality,
}: LocalitySelectorSheetProps): React.ReactElement {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<LocalitySearchResult[]>([]);
  const [searching, setSearching] = useState(false);
  const searchTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  const handleSearchChange = (text: string) => {
    setSearchQuery(text);
    if (searchTimeout.current) clearTimeout(searchTimeout.current);
    if (text.trim().length < 2) {
      setSearchResults([]);
      return;
    }
    searchTimeout.current = setTimeout(async () => {
      setSearching(true);
      try {
        const result = await searchLocalities(text.trim());
        if (result.ok) setSearchResults(result.value);
      } finally {
        setSearching(false);
      }
    }, 350);
  };

  const showFollowed = searchQuery.trim().length < 2;

  // Normalise both data sources into a single shape for FlatList
  const listData: Array<{ localityId: string; label: string }> = showFollowed
    ? followedLocalities.map((l) => ({
        localityId: l.localityId,
        label: l.displayLabel ?? l.wardName,
      }))
    : searchResults.map((r) => ({
        localityId: r.localityId,
        label: r.placeLabel,
      }));

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
          <Text style={styles.sheetTitle}>Your areas</Text>
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
            placeholder="Search for an area…"
            placeholderTextColor={Colors.faintForeground}
            value={searchQuery}
            onChangeText={handleSearchChange}
            autoCorrect={false}
            returnKeyType="search"
          />
          {searching && (
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
          keyExtractor={(item) => item.localityId}
          renderItem={({ item }) => {
            const id = item.localityId;
            const label = item.label;
            const isActive = id === activeLocalityId;

            return (
              <TouchableOpacity
                style={[styles.localityRow, isActive && styles.localityRowActive]}
                onPress={() => {
                  Keyboard.dismiss();
                  onSelectLocality(id);
                }}
                accessibilityRole="button"
                accessibilityState={{ selected: isActive }}
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
            searchQuery.trim().length >= 2 && !searching ? (
              <Text style={styles.searchEmpty}>No areas found</Text>
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
    maxHeight: 280,
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
