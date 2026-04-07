// apps/citizen-mobile/app/(app)/clusters/[id].tsx
//
// Cluster detail screen.
//
// Loads GET /v1/clusters/{id} via the useCluster hook (which unwraps
// the Result<T, ApiError> from src/api/clusters.ts).
//
// Participation rules implemented here:
//
//   - The cluster response does NOT tell the client whether the current
//     user has already voted Affected/Observing. So participation state
//     is session-scoped: the user only sees their own state if they
//     tapped a button during this session.
//
//   - "Add Further Context" appears ONLY after the user taps "I'm Affected"
//     and ONLY within the 2-minute backend window. We track the timestamp
//     locally (the cluster response doesn't expose it either) and re-render
//     once a second so the affordance disappears at exactly t+2:00.
//     If the user manages to submit late we still gracefully handle the
//     server-side 422 context_edit_window_expired.
//
//   - Restoration banner: shown only when state === 'possible_restoration'.
//     The backend does NOT return restoration vote counts in the cluster
//     response, so we cannot show "72% of affected people say resolved" —
//     just a status banner with a CTA into the restoration modal.
//
//   - Official posts render alongside citizen content (always after the
//     participation actions, never replacing the citizen view).

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  TextInput,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { useCluster, useParticipation, useAddContext } from '../../../src/hooks/useClusters';
import { ClusterStateBadge } from '../../../src/components/clusters/ClusterStateBadge';
import { ParticipationBar } from '../../../src/components/cluster/ParticipationBar';
import { Loading } from '../../../src/components/common/Loading';
import {
  formatRelativeTime,
  formatCategoryLabel,
} from '../../../src/utils/formatters';
import {
  isContextWindowOpen,
  secondsRemaining,
} from '../../../src/utils/contextWindow';
import { CONTEXT_TEXT_MAX_LENGTH } from '../../../src/config/constants';
import type {
  ClusterResponse,
  OfficialPostResponse,
  ParticipationType,
} from '../../../src/types/api';

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

export default function ClusterDetailScreen(): React.ReactElement {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();

  const clusterId = id ?? '';
  const clusterQuery = useCluster(clusterId);
  const participationMutation = useParticipation(clusterId);
  const contextMutation = useAddContext(clusterId);

  // ── Session-local participation state ─────────────────────────────────────
  const [localParticipation, setLocalParticipation] =
    useState<LocalParticipation>(null);
  const [affectedAt, setAffectedAt] = useState<number | null>(null);
  const [now, setNow] = useState<number>(Date.now());
  const [contextText, setContextText] = useState('');
  const [contextSubmitted, setContextSubmitted] = useState(false);
  const [contextError, setContextError] = useState<string | null>(null);
  const [participationError, setParticipationError] = useState<string | null>(
    null,
  );

  // Tick the clock once per second while the context window is open so the
  // "Add Further Context" affordance disappears exactly at the boundary.
  const windowOpen = isContextWindowOpen(affectedAt, now);
  useEffect(() => {
    if (affectedAt === null) return;
    if (!windowOpen) return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [affectedAt, windowOpen]);

  // ── Participation handler ─────────────────────────────────────────────────
  const handleParticipate = useCallback(
    async (type: ParticipationType): Promise<void> => {
      setParticipationError(null);
      try {
        const deviceHash = await getDeviceHash();
        const idempotencyKey = await Crypto.digestStringAsync(
          Crypto.CryptoDigestAlgorithm.SHA256,
          `${clusterId}:${type}:${Date.now()}`,
        );
        await participationMutation.mutateAsync({
          type,
          deviceHash,
          idempotencyKey,
        });

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
    [clusterId, participationMutation],
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
      // Server-side window check is the source of truth — surface the
      // specific error if we somehow submitted late. The 422 envelope now
      // uses code=policy_blocked with a specific reason string; we still
      // accept the legacy direct codes for forward compatibility.
      if (err instanceof Error) {
        if (/context_edit_window_expired/.test(err.message)) {
          setContextError('The 2-minute context window has closed.');
        } else if (/context_requires_affected/.test(err.message)) {
          setContextError(
            "You need to mark yourself as affected before adding context.",
          );
        } else if (/policy_blocked/.test(err.message)) {
          setContextError(
            'You can no longer add context to this cluster.',
          );
        } else {
          setContextError(err.message);
        }
      } else {
        setContextError('Could not submit context. Please try again.');
      }
    }
  }, [contextText, contextMutation]);

  // ── Render ────────────────────────────────────────────────────────────────
  if (clusterQuery.isLoading && !clusterQuery.data) {
    return <Loading />;
  }
  if (clusterQuery.isError || !clusterQuery.data) {
    return (
      <ErrorState
        message={
          clusterQuery.error?.message ?? 'Could not load this cluster.'
        }
        onRetry={() => void clusterQuery.refetch()}
        onBack={() => router.back()}
      />
    );
  }

  const cluster = clusterQuery.data;

  // Server is the source of truth for both restricted CTAs. The local
  // `localParticipation` / `windowOpen` state is kept as a UX hint (for the
  // brief gap between mutation success and refetch) but is NOT sufficient
  // on its own — see PR #50 follow-up Task 3 + the matching server-side
  // 422 policy_blocked check in ClustersController.
  const myP = cluster.myParticipation;
  const isPossibleRestoration = cluster.state === 'possible_restoration';
  // Restoration response CTA: only when the caller is currently affected.
  // Without `myParticipation` (unauthenticated or never participated) the
  // banner is hidden even if the cluster is in possible_restoration.
  const canShowRestorationBanner =
    isPossibleRestoration && myP?.type === 'affected';
  // Add Further Context: server's canAddContext is authoritative.
  const showContextBlock =
    myP?.canAddContext === true && !contextSubmitted;

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
        <Text style={styles.navTitle} numberOfLines={1}>
          {formatCategoryLabel(cluster.category)}
        </Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView style={styles.flex} contentContainerStyle={styles.content}>
        <View style={styles.titleRow}>
          <Text style={styles.title}>
            {cluster.title ?? formatCategoryLabel(cluster.category)}
          </Text>
          <ClusterStateBadge state={cluster.state} />
        </View>

        {cluster.summary !== null && cluster.summary !== '' && (
          <Text style={styles.summary}>{cluster.summary}</Text>
        )}

        <ParticipationBar
          affectedCount={cluster.affectedCount}
          observingCount={cluster.observingCount}
        />

        {/* Restoration banner — gated by myParticipation.type === 'affected'.
            Server enforces the same rule on POST and returns 422
            policy_blocked / restoration_requires_affected_participation. */}
        {canShowRestorationBanner && (
          <RestorationBanner
            onPress={() =>
              router.push(`/(modals)/restoration/${cluster.id}`)
            }
          />
        )}

        {/* Action buttons */}
        <View style={styles.actions}>
          <Text style={styles.actionsTitle}>How does this affect you?</Text>
          <View style={styles.actionRow}>
            <ActionButton
              label="I'm Affected"
              isActive={localParticipation === 'affected'}
              isDisabled={participationMutation.isPending}
              onPress={() => void handleParticipate('affected')}
            />
            <ActionButton
              label="I'm Observing"
              isActive={localParticipation === 'observing'}
              isDisabled={participationMutation.isPending}
              onPress={() => void handleParticipate('observing')}
            />
          </View>

          {localParticipation !== null && (
            <TouchableOpacity
              onPress={() => void handleParticipate('no_longer_affected')}
              disabled={participationMutation.isPending}
              style={styles.ghostButton}
              accessible
              accessibilityRole="button"
              accessibilityLabel="No longer affected"
            >
              <Text style={styles.ghostButtonText}>No longer affected</Text>
            </TouchableOpacity>
          )}

          {participationError !== null && (
            <Text style={styles.errorText} accessibilityRole="alert">
              {participationError}
            </Text>
          )}
        </View>

        {/* Add Further Context — only inside the 2-minute window */}
        {showContextBlock && (
          <ContextBlock
            text={contextText}
            onChangeText={(t) =>
              setContextText(t.slice(0, CONTEXT_TEXT_MAX_LENGTH))
            }
            secondsLeft={secondsRemaining(affectedAt, now)}
            onSubmit={() => void handleSubmitContext()}
            isPending={contextMutation.isPending}
            error={contextError}
          />
        )}

        {contextSubmitted && (
          <Text style={styles.contextDone}>
            Context submitted. Thank you.
          </Text>
        )}

        {/* Official posts — alongside, never replacing citizen content */}
        {cluster.officialPosts.length > 0 && (
          <View style={styles.officialSection}>
            <Text style={styles.sectionTitle}>Official response</Text>
            {cluster.officialPosts.map((post) => (
              <OfficialPostCard key={post.id} post={post} />
            ))}
          </View>
        )}

        <Text style={styles.updated}>
          Updated {formatRelativeTime(cluster.updatedAt)}
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function ActionButton({
  label,
  isActive,
  isDisabled,
  onPress,
}: {
  label: string;
  isActive: boolean;
  isDisabled: boolean;
  onPress: () => void;
}): React.ReactElement {
  return (
    <TouchableOpacity
      style={[
        styles.actionBtn,
        isActive ? styles.actionBtnActive : styles.actionBtnInactive,
        isDisabled && styles.actionBtnDisabled,
      ]}
      onPress={onPress}
      disabled={isDisabled}
      accessible
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: isActive, disabled: isDisabled }}
    >
      <Text
        style={[
          styles.actionBtnText,
          isActive
            ? styles.actionBtnTextActive
            : styles.actionBtnTextInactive,
        ]}
      >
        {label}
      </Text>
    </TouchableOpacity>
  );
}

function RestorationBanner({
  onPress,
}: {
  onPress: () => void;
}): React.ReactElement {
  return (
    <TouchableOpacity
      style={styles.restorationBanner}
      onPress={onPress}
      accessible
      accessibilityRole="button"
      accessibilityLabel="Has this been resolved?"
      accessibilityHint="Open the restoration response prompt"
    >
      <View style={styles.restorationTextWrap}>
        <Text style={styles.restorationTitle}>
          Voting is open — has this been resolved?
        </Text>
        <Text style={styles.restorationSub}>
          Let us know if service has been restored for you.
        </Text>
      </View>
      <Ionicons name="chevron-forward" size={18} color="#1E40AF" />
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
        Optional — describe what you&apos;re experiencing (max{' '}
        {CONTEXT_TEXT_MAX_LENGTH} chars).
      </Text>
      <TextInput
        style={styles.contextInput}
        value={text}
        onChangeText={onChangeText}
        placeholder="e.g. Road completely blocked, cars diverting through side streets…"
        placeholderTextColor="#9CA3AF"
        multiline
        numberOfLines={3}
        textAlignVertical="top"
        editable={!isPending}
        accessible
        accessibilityLabel="Further context text"
      />
      <Text style={styles.contextCounter}>
        {text.length}/{CONTEXT_TEXT_MAX_LENGTH}
      </Text>
      <TouchableOpacity
        style={[styles.submitBtn, !canSubmit && styles.submitBtnDisabled]}
        onPress={onSubmit}
        disabled={!canSubmit}
        accessible
        accessibilityRole="button"
        accessibilityLabel="Submit context"
      >
        {isPending ? (
          <ActivityIndicator color="#FFFFFF" size="small" />
        ) : (
          <Text style={styles.submitBtnText}>Submit context</Text>
        )}
      </TouchableOpacity>
      {error !== null && (
        <Text style={styles.errorText} accessibilityRole="alert">
          {error}
        </Text>
      )}
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
      <Text style={styles.officialBody}>{post.body}</Text>
      <Text style={styles.officialTime}>
        {formatRelativeTime(post.createdAt)}
      </Text>
    </View>
  );
}

function ErrorState({
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
        <TouchableOpacity
          style={styles.primaryCta}
          onPress={onRetry}
          accessible
          accessibilityRole="button"
        >
          <Text style={styles.primaryCtaText}>Try again</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.secondaryCta}
          onPress={onBack}
          accessible
          accessibilityRole="button"
        >
          <Text style={styles.secondaryCtaText}>Go back</Text>
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

// ─── Styles ──────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#F9FAFB' },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: '#E5E7EB',
  },
  navTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#111827',
    flex: 1,
    textAlign: 'center',
    marginHorizontal: 8,
  },
  navSpacer: { width: 24 },
  content: { padding: 16, gap: 16, paddingBottom: 40 },
  titleRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: 8,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#111827',
    flex: 1,
  },
  summary: { fontSize: 15, color: '#374151', lineHeight: 22 },
  actions: { gap: 10 },
  actionsTitle: { fontSize: 15, fontWeight: '600', color: '#374151' },
  actionRow: { flexDirection: 'row', gap: 10 },
  actionBtn: {
    flex: 1,
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: 'center',
    borderWidth: 1.5,
  },
  actionBtnActive: {
    backgroundColor: '#1a3a2f',
    borderColor: '#1a3a2f',
  },
  actionBtnInactive: {
    backgroundColor: '#FFFFFF',
    borderColor: '#1a3a2f',
  },
  actionBtnDisabled: { opacity: 0.6 },
  actionBtnText: { fontSize: 15, fontWeight: '600' },
  actionBtnTextActive: { color: '#FFFFFF' },
  actionBtnTextInactive: { color: '#1a3a2f' },
  ghostButton: {
    paddingVertical: 12,
    alignItems: 'center',
  },
  ghostButtonText: {
    fontSize: 14,
    color: '#6B7280',
    fontWeight: '500',
  },
  errorText: { fontSize: 14, color: '#DC2626' },
  contextBlock: {
    backgroundColor: '#F0FDF4',
    borderRadius: 12,
    padding: 14,
    gap: 10,
    borderWidth: 1,
    borderColor: '#BBF7D0',
  },
  contextHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  contextTitle: { fontSize: 16, fontWeight: '600', color: '#166534' },
  contextTimer: { fontSize: 13, color: '#166534', fontVariant: ['tabular-nums'] },
  contextSub: { fontSize: 13, color: '#166534' },
  contextInput: {
    borderWidth: 1.5,
    borderColor: '#86EFAC',
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    color: '#111827',
    backgroundColor: '#FFFFFF',
    minHeight: 80,
  },
  contextCounter: { fontSize: 12, color: '#6B7280', textAlign: 'right' },
  contextDone: {
    fontSize: 14,
    color: '#166534',
    textAlign: 'center',
    fontStyle: 'italic',
  },
  submitBtn: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
  },
  submitBtnDisabled: { backgroundColor: '#9CA3AF' },
  submitBtnText: { color: '#FFFFFF', fontSize: 15, fontWeight: '600' },
  restorationBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#EFF6FF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#BFDBFE',
  },
  restorationTextWrap: { flex: 1 },
  restorationTitle: { fontSize: 15, fontWeight: '600', color: '#1E40AF' },
  restorationSub: { fontSize: 13, color: '#1E3A8A', marginTop: 2 },
  officialSection: { gap: 10 },
  sectionTitle: { fontSize: 17, fontWeight: '700', color: '#111827' },
  officialCard: {
    backgroundColor: '#FFFBEB',
    borderRadius: 10,
    padding: 14,
    gap: 6,
    borderLeftWidth: 3,
    borderLeftColor: '#F59E0B',
  },
  officialTitle: { fontSize: 15, fontWeight: '600', color: '#111827' },
  officialBody: { fontSize: 14, color: '#374151', lineHeight: 20 },
  officialTime: { fontSize: 12, color: '#9CA3AF' },
  updated: { fontSize: 12, color: '#9CA3AF', textAlign: 'center' },
  errorContainer: {
    flex: 1,
    padding: 24,
    gap: 12,
    justifyContent: 'center',
  },
  errorTitle: { fontSize: 20, fontWeight: '700', color: '#111827' },
  errorBody: { fontSize: 14, color: '#6B7280', lineHeight: 20 },
  primaryCta: {
    marginTop: 12,
    backgroundColor: '#1a3a2f',
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: 'center',
  },
  primaryCtaText: { color: '#FFFFFF', fontSize: 15, fontWeight: '600' },
  secondaryCta: { paddingVertical: 12, alignItems: 'center' },
  secondaryCtaText: { fontSize: 14, color: '#6B7280' },
});
