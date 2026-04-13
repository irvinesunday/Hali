// apps/citizen-mobile/app/(app)/clusters/[id].tsx
//
// Cluster detail screen.
//
// Participation rules (do not change):
//   - The API echoes the caller's participation via `cluster.myParticipation`
//     (type + canAddContext) and the server is the source of truth for
//     gating Add-Context and the restoration banner. The local
//     `localParticipation` state below is only used for the *button highlight*
//     between mutate→refetch (a UX hint, not authorization).
//   - "Add Further Context" only inside the 2-minute window, only after Affected.
//   - Restoration banner only when state === 'possible_restoration' AND
//     myParticipation.type === 'affected'.
//   - Official posts render alongside citizen content, never replacing it.
//   - "I'm Observing" is hidden for experiential categories (electricity, water).
//   - Unauthenticated users are navigated to auth when they attempt to participate.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  TextInput,
  StyleSheet,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft, ChevronRight } from 'lucide-react-native';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';

import { useCluster, useParticipation, useAddContext } from '../../../src/hooks/useClusters';
import { useAuth } from '../../../src/context/AuthContext';
import { ClusterStateBadge } from '../../../src/components/clusters/ClusterStateBadge';
import { ParticipationBar } from '../../../src/components/cluster/ParticipationBar';
import { Loading } from '../../../src/components/common/Loading';
import { Button } from '../../../src/components/common/Button';
import {
  AuthPrompt,
  CategoryIconCircle,
  SectionHeader,
  OfficialUpdateRow,
  FeedbackButton,
} from '../../../src/components/shared';

import {
  formatRelativeTime,
  formatCategoryLabel,
  getCategoryInstitutionName,
} from '../../../src/utils/formatters';
import {
  isContextWindowOpen,
  secondsRemaining,
} from '../../../src/utils/contextWindow';
import { CONTEXT_TEXT_MAX_LENGTH } from '../../../src/config/constants';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
  ScreenPaddingH,
  ScreenPaddingBottom,
} from '../../../src/theme';
import type { ParticipationType } from '../../../src/types/api';

// ─── Experiential categories — no "I'm Observing" option ─────────────────────
const EXPERIENTIAL_CATEGORIES = new Set(['electricity', 'water']);

type LocalParticipation = 'affected' | 'observing' | null;

async function getDeviceHash(): Promise<string> {
  const parts = [
    Device.modelName ?? 'unknown',
    Device.osName ?? 'unknown',
    Device.osVersion ?? 'unknown',
    Device.brand ?? 'unknown',
    Application.applicationId ?? 'unknown',
  ].join('|');
  return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, parts);
}

// ─── Screen ──────────────────────────────────────────────────────────────────

export default function ClusterDetailScreen(): React.ReactElement {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { authState } = useAuth();

  const clusterId = id ?? '';
  const clusterQuery = useCluster(clusterId);
  const participationMutation = useParticipation(clusterId);
  const contextMutation = useAddContext(clusterId);

  // ── Session-local participation state ────────────────────────────────────
  const [localParticipation, setLocalParticipation] =
    useState<LocalParticipation>(null);
  const [affectedAt, setAffectedAt] = useState<number | null>(null);
  const [now, setNow] = useState<number>(Date.now());
  const [contextText, setContextText] = useState('');
  const [contextSubmitted, setContextSubmitted] = useState(false);
  const [contextError, setContextError] = useState<string | null>(null);
  const [participationError, setParticipationError] = useState<string | null>(null);

  // Clock tick for the context window countdown
  const windowOpen = isContextWindowOpen(affectedAt, now);
  useEffect(() => {
    if (affectedAt === null || !windowOpen) return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [affectedAt, windowOpen]);

  // ── Auth gate ──────────────────────────────────────────────────────────────
  // Guard against stacking multiple phone screens if the user taps a
  // participation button repeatedly while unauthenticated. The flag is
  // cleared once auth status changes (login completed or screen revisited).
  const navigatingToAuthRef = useRef<boolean>(false);
  useEffect(() => {
    if (authState.status === 'authenticated') {
      navigatingToAuthRef.current = false;
    }
  }, [authState.status]);
  const requireAuth = useCallback((): boolean => {
    if (authState.status === 'authenticated') return true;
    if (navigatingToAuthRef.current) return false;
    navigatingToAuthRef.current = true;
    // Navigate to phone auth — Expo Router will return here on back.
    router.push('/(auth)/phone');
    return false;
  }, [authState.status, router]);

  // ── Participation handler ─────────────────────────────────────────────────
  const handleParticipate = useCallback(
    async (type: ParticipationType): Promise<void> => {
      if (!requireAuth()) return;
      setParticipationError(null);
      try {
        const deviceHash = await getDeviceHash();
        const idempotencyKey = await Crypto.digestStringAsync(
          Crypto.CryptoDigestAlgorithm.SHA256,
          `${clusterId}:${type}:${Date.now()}`,
        );
        await participationMutation.mutateAsync({ type, deviceHash, idempotencyKey });
        if (type === 'affected') {
          setLocalParticipation('affected');
          setAffectedAt(Date.now());
          setNow(Date.now());
          setContextSubmitted(false);
          setContextText('');
        } else if (type === 'observing') {
          setLocalParticipation('observing');
          setAffectedAt(null);
        } else {
          setLocalParticipation(null);
          setAffectedAt(null);
        }
      } catch (err) {
        setParticipationError(
          err instanceof Error
            ? err.message
            : 'Could not record your response. Please try again.',
        );
      }
    },
    [clusterId, participationMutation, requireAuth],
  );

  // ── Context handler ───────────────────────────────────────────────────────
  const handleSubmitContext = useCallback(async (): Promise<void> => {
    const trimmed = contextText.trim();
    if (trimmed === '') return;
    setContextError(null);
    try {
      const deviceHash = await getDeviceHash();
      await contextMutation.mutateAsync({ text: trimmed, deviceHash });
      setContextSubmitted(true);
    } catch (err) {
      if (err instanceof Error) {
        if (/context_edit_window_expired/.test(err.message)) {
          setContextError('The 2-minute context window has closed.');
        } else if (/context_requires_affected/.test(err.message)) {
          setContextError('You need to mark yourself as affected before adding context.');
        } else if (/policy_blocked/.test(err.message)) {
          setContextError('You can no longer add context to this cluster.');
        } else {
          setContextError(err.message);
        }
      } else {
        setContextError('Could not submit context. Please try again.');
      }
    }
  }, [contextText, contextMutation]);

  // ── Render ───────────────────────────────────────────────────────────────
  if (clusterQuery.isLoading && !clusterQuery.data) {
    return <Loading />;
  }
  if (clusterQuery.isError || !clusterQuery.data) {
    return (
      <DetailErrorState
        message={clusterQuery.error?.message ?? 'Could not load this cluster.'}
        onRetry={() => void clusterQuery.refetch()}
        onBack={() => router.back()}
      />
    );
  }

  const cluster = clusterQuery.data;
  const myP = cluster.myParticipation;
  const isExperiential = EXPERIENTIAL_CATEGORIES.has(cluster.category);
  const isPossibleRestoration = cluster.state === 'possible_restoration';
  const canShowRestorationBanner = isPossibleRestoration && myP?.type === 'affected';
  const showContextBlock = myP?.canAddContext === true && !contextSubmitted;
  const institutionName = getCategoryInstitutionName(cluster.category);

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>

      {/* ── Nav bar ──────────────────────────────────────────────── */}
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessibilityRole="button"
          accessibilityLabel="Back"
        >
          <ArrowLeft size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>
        <Text style={styles.navTitle} numberOfLines={1}>
          {formatCategoryLabel(cluster.category)}
        </Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView
        style={styles.flex}
        contentContainerStyle={styles.content}
        showsVerticalScrollIndicator={false}
      >

        {/* ── Header block ─────────────────────────────────────── */}
        <View style={styles.headerBlock}>
          <CategoryIconCircle category={cluster.category} size="lg" />
          <View style={styles.headerText}>
            <Text style={styles.title}>
              {cluster.title ?? formatCategoryLabel(cluster.category)}
            </Text>
            <View style={styles.badgeRow}>
              <ClusterStateBadge state={cluster.state} />
              {/* TODO: render ConditionBadge here once ClusterResponse exposes
                  a dominantConditionSlug field (currently only subcategorySlug
                  is available, which is not a condition label). */}
            </View>
          </View>
        </View>

        {/* ── Summary ──────────────────────────────────────────── */}
        {cluster.summary !== null && cluster.summary !== '' && (
          <Text style={styles.summary}>{cluster.summary}</Text>
        )}

        {/* ── Institution attribution ───────────────────────────── */}
        <Text style={styles.institution}>
          {institutionName} · {formatRelativeTime(cluster.updatedAt)}
        </Text>

        {/* ── Participation counts ──────────────────────────────── */}
        <ParticipationBar
          affectedCount={cluster.affectedCount}
          observingCount={cluster.observingCount}
        />

        {/* ── Restoration banner ────────────────────────────────── */}
        {canShowRestorationBanner && (
          <RestorationBanner
            onPress={() => router.push(`/(modals)/restoration/${cluster.id}`)}
          />
        )}

        {/* ── Participation actions ─────────────────────────────── */}
        {authState.status !== 'authenticated' ? (
          <AuthPrompt
            message="Sign in to report how this affects you and contribute to this signal."
            onSignIn={() => {
              void requireAuth();
            }}
          />
        ) : (
          <View style={styles.actionsBlock}>
            <Text style={styles.actionsLabel}>How does this affect you?</Text>
            <View style={styles.actionRow}>
              <Button
                label="I'm Affected"
                variant={localParticipation === 'affected' ? 'primary' : 'secondary'}
                size="sm"
                loading={
                  participationMutation.isPending &&
                  participationMutation.variables?.type === 'affected'
                }
                disabled={participationMutation.isPending}
                onPress={() => void handleParticipate('affected')}
                style={styles.actionBtn}
              />
              {!isExperiential && (
                <Button
                  label="I'm Observing"
                  variant={localParticipation === 'observing' ? 'primary' : 'secondary'}
                  size="sm"
                  loading={
                    participationMutation.isPending &&
                    participationMutation.variables?.type === 'observing'
                  }
                  disabled={participationMutation.isPending}
                  onPress={() => void handleParticipate('observing')}
                  style={styles.actionBtn}
                />
              )}
            </View>

            {localParticipation !== null && (
              <TouchableOpacity
                onPress={() => void handleParticipate('no_longer_affected')}
                disabled={participationMutation.isPending}
                style={styles.ghostCta}
                accessibilityRole="button"
                accessibilityLabel="No longer affected"
              >
                <Text style={styles.ghostCtaText}>No longer affected</Text>
              </TouchableOpacity>
            )}

            {participationError !== null && (
              <Text style={styles.errorText} accessibilityRole="alert">
                {participationError}
              </Text>
            )}
          </View>
        )}

        {/* ── Add Further Context ───────────────────────────────── */}
        {showContextBlock && (
          <ContextBlock
            text={contextText}
            onChangeText={(t) => setContextText(t.slice(0, CONTEXT_TEXT_MAX_LENGTH))}
            secondsLeft={secondsRemaining(affectedAt, now)}
            onSubmit={() => void handleSubmitContext()}
            isPending={contextMutation.isPending}
            error={contextError}
          />
        )}

        {contextSubmitted && (
          <Text style={styles.contextDone}>Context submitted. Thank you.</Text>
        )}

        {/* ── Official posts ────────────────────────────────────── */}
        {cluster.officialPosts.length > 0 && (
          <View style={styles.officialSection}>
            <SectionHeader label="Official Response" />
            <View style={styles.officialList}>
              {cluster.officialPosts.map((post) => (
                <OfficialUpdateRow
                  key={post.id}
                  institutionName={getCategoryInstitutionName(post.category)}
                  message={post.title}
                />
              ))}
            </View>
          </View>
        )}

      </ScrollView>

      {/* ── Feedback button ───────────────────────────────────── */}
      <FeedbackButton screen="detail" clusterId={clusterId} />

    </SafeAreaView>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function RestorationBanner({
  onPress,
}: {
  onPress: () => void;
}): React.ReactElement {
  return (
    <TouchableOpacity
      style={styles.restorationBanner}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel="Has this been resolved?"
      accessibilityHint="Open the restoration response prompt"
    >
      <View style={styles.restorationTextWrap}>
        <Text style={styles.restorationTitle}>Voting is open — has this been resolved?</Text>
        <Text style={styles.restorationSub}>
          Let us know if service has been restored for you.
        </Text>
      </View>
      <ChevronRight size={18} color={Colors.emerald} strokeWidth={2} />
    </TouchableOpacity>
  );
}

function ContextBlock({
  text,
  onChangeText,
  secondsLeft,
  onSubmit,
  isPending,
  error,
}: {
  text: string;
  onChangeText: (s: string) => void;
  secondsLeft: number;
  onSubmit: () => void;
  isPending: boolean;
  error: string | null;
}): React.ReactElement {
  const minutes = Math.floor(secondsLeft / 60);
  const seconds = secondsLeft % 60;
  const timeLabel = `${minutes}:${seconds.toString().padStart(2, '0')}`;
  const canSubmit = text.trim().length > 0 && !isPending;

  return (
    <View style={styles.contextBlock}>
      <View style={styles.contextHeader}>
        <Text style={styles.contextTitle}>Add further context</Text>
        <Text style={styles.contextTimer}>{timeLabel} left</Text>
      </View>
      <Text style={styles.contextSub}>
        Optional — describe what you&apos;re experiencing (max {CONTEXT_TEXT_MAX_LENGTH} chars).
      </Text>
      <TextInput
        style={styles.contextInput}
        value={text}
        onChangeText={onChangeText}
        placeholder="e.g. Road completely blocked, cars diverting through side streets…"
        placeholderTextColor={Colors.faintForeground}
        multiline
        numberOfLines={3}
        textAlignVertical="top"
        editable={!isPending}
        accessibilityLabel="Further context text"
      />
      <Text style={styles.contextCounter}>
        {text.length}/{CONTEXT_TEXT_MAX_LENGTH}
      </Text>
      <Button
        label="Submit context"
        onPress={onSubmit}
        disabled={!canSubmit}
        loading={isPending}
      />
      {error !== null && (
        <Text style={styles.errorText} accessibilityRole="alert">
          {error}
        </Text>
      )}
    </View>
  );
}

function DetailErrorState({
  message,
  onRetry,
  onBack,
}: {
  message: string;
  onRetry: () => void;
  onBack: () => void;
}): React.ReactElement {
  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.errorContainer}>
        <Text style={styles.errorTitle}>Couldn&apos;t load this cluster</Text>
        <Text style={styles.errorBody}>{message}</Text>
        <Button label="Try again" onPress={onRetry} style={styles.errorCta} />
        <Button
          label="Go back"
          variant="ghost"
          onPress={onBack}
          style={styles.errorCta}
        />
      </View>
    </SafeAreaView>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.background },
  flex: { flex: 1 },

  // Nav bar
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: ScreenPaddingH,
    paddingVertical: Spacing.md,
    backgroundColor: Colors.card,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  navTitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
    flex: 1,
    textAlign: 'center',
    marginHorizontal: Spacing.sm,
  },
  navSpacer: { width: 24 },

  // Content
  content: {
    paddingHorizontal: ScreenPaddingH,
    paddingTop: Spacing.lg,
    paddingBottom: ScreenPaddingBottom,
    gap: Spacing.lg,
  },

  // Header block
  headerBlock: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.md,
  },
  headerText: { flex: 1, gap: Spacing.sm },
  title: {
    fontSize: FontSize.title,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  badgeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.xs,
  },

  // Summary + meta
  summary: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    lineHeight: FontSize.body * 1.5,
  },
  institution: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
  },

  // Participation actions
  actionsBlock: { gap: Spacing.md },
  actionsLabel: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.mutedForeground,
  },
  actionRow: { flexDirection: 'row', gap: Spacing.md },
  actionBtn: { flex: 1 },
  ghostCta: { paddingVertical: Spacing.sm, alignItems: 'center' },
  ghostCtaText: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.medium,
    color: Colors.mutedForeground,
  },
  errorText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
  },

  // Restoration banner
  restorationBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: Colors.emeraldSubtle,
    borderRadius: Radius.lg,
    padding: Spacing.lg,
    borderWidth: 1,
    borderColor: Colors.emerald + '40',
  },
  restorationTextWrap: { flex: 1 },
  restorationTitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.emerald,
  },
  restorationSub: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.emerald,
    marginTop: 2,
    opacity: 0.8,
  },

  // Context block
  contextBlock: {
    backgroundColor: Colors.emeraldSubtle,
    borderRadius: Radius.lg,
    padding: Spacing.lg,
    gap: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.emerald + '40',
  },
  contextHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  contextTitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.semiBold,
    color: Colors.emerald,
  },
  contextTimer: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.emerald,
    fontVariant: ['tabular-nums'],
  },
  contextSub: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.emerald,
    opacity: 0.9,
  },
  contextInput: {
    borderWidth: 1.5,
    borderColor: Colors.emerald + '60',
    borderRadius: Radius.sm,
    padding: Spacing.md,
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
    backgroundColor: Colors.card,
    minHeight: 80,
  },
  contextCounter: {
    fontSize: FontSize.micro,
    fontFamily: FontFamily.regular,
    color: Colors.faintForeground,
    textAlign: 'right',
  },
  contextDone: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.emerald,
    textAlign: 'center',
    fontStyle: 'italic',
  },

  // Official section
  officialSection: { gap: Spacing.md },
  officialList: { gap: Spacing.sm },

  // Error state
  errorContainer: {
    flex: 1,
    padding: ScreenPaddingH,
    gap: Spacing.md,
    justifyContent: 'center',
  },
  errorTitle: {
    fontSize: FontSize.title,
    fontFamily: FontFamily.bold,
    color: Colors.foreground,
  },
  errorBody: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    lineHeight: FontSize.body * 1.5,
  },
  errorCta: { marginTop: Spacing.xs },
});
