// Cluster Detail screen — participation bar, actions, official updates
import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  TextInput,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import * as Device from 'expo-device';
import * as Application from 'expo-application';
import { useCluster, useParticipation, useAddContext } from '../../../src/hooks/useClusters';
import { ClusterStateBadge } from '../../../src/components/clusters/ClusterStateBadge';
import { ParticipationBar } from '../../../src/components/clusters/ParticipationBar';
import { Loading } from '../../../src/components/common/Loading';
import { Button } from '../../../src/components/common/Button';
import { formatRelativeTime, formatCategoryLabel } from '../../../src/utils/formatters';
import { PARTICIPATION_LABELS, CONTEXT_TEXT_MAX_LENGTH } from '../../../src/config/constants';

export default function ClusterDetailScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: cluster, isLoading } = useCluster(id);
  const participation = useParticipation(id);
  const addContext = useAddContext(id);

  const [activeParticipation, setActiveParticipation] = useState<
    'affected' | 'observing' | null
  >(null);
  const [showContext, setShowContext] = useState(false);
  const [contextText, setContextText] = useState('');
  const [contextSubmitted, setContextSubmitted] = useState(false);

  async function getDeviceHash(): Promise<string> {
    const raw = `${Device.osName}-${Device.osVersion}-${Device.modelName}-${Application.applicationId}`;
    return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, raw);
  }

  async function handleParticipate(
    type: 'affected' | 'observing' | 'no_longer_affected',
  ) {
    const deviceHash = await getDeviceHash();
    const idempotencyKey = await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256,
      `${id}:${type}:${Date.now()}`,
    );
    participation.mutate(
      { type, deviceHash, idempotencyKey },
      {
        onSuccess: () => {
          if (type === 'affected') {
            setActiveParticipation('affected');
            setShowContext(true);
          } else if (type === 'observing') {
            setActiveParticipation('observing');
            setShowContext(false);
          } else {
            setActiveParticipation(null);
            setShowContext(false);
          }
        },
        onError: () => {
          Alert.alert('Error', 'Could not record participation. Please try again.');
        },
      },
    );
  }

  async function handleSubmitContext() {
    if (!contextText.trim()) return;
    const deviceHash = await getDeviceHash();
    addContext.mutate(
      { text: contextText.trim(), deviceHash },
      {
        onSuccess: () => {
          setContextSubmitted(true);
          setShowContext(false);
        },
        onError: () => {
          Alert.alert('Error', 'Could not submit context. Please try again.');
        },
      },
    );
  }

  if (isLoading || !cluster) return <Loading />;

  const isRestoration = cluster.state === 'possible_restoration';

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      {/* Header */}
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}>
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle} numberOfLines={1}>
          {formatCategoryLabel(cluster.category)}
        </Text>
        <View style={{ width: 24 }} />
      </View>

      <ScrollView
        style={styles.flex}
        contentContainerStyle={styles.content}
      >
        {/* Category + state */}
        <View style={styles.titleRow}>
          <Text style={styles.category}>
            {formatCategoryLabel(cluster.category)}
          </Text>
          <ClusterStateBadge state={cluster.state} />
        </View>

        {cluster.summary ? (
          <Text style={styles.summary}>{cluster.summary}</Text>
        ) : null}

        {/* 1 — Participation bar */}
        <ParticipationBar
          affectedCount={cluster.affectedCount}
          observingCount={cluster.observingCount}
        />

        {/* 2 — Participation actions */}
        <View style={styles.actions}>
          <Text style={styles.actionsTitle}>How does this affect you?</Text>

          <View style={styles.actionRow}>
            <Button
              label={PARTICIPATION_LABELS.affected}
              variant={activeParticipation === 'affected' ? 'primary' : 'secondary'}
              onPress={() => handleParticipate('affected')}
              loading={participation.isPending}
              style={styles.actionBtn}
            />
            <Button
              label={PARTICIPATION_LABELS.observing}
              variant={activeParticipation === 'observing' ? 'primary' : 'secondary'}
              onPress={() => handleParticipate('observing')}
              loading={participation.isPending}
              style={styles.actionBtn}
            />
          </View>

          {activeParticipation !== null && (
            <Button
              label={PARTICIPATION_LABELS.no_longer_affected}
              variant="ghost"
              onPress={() => handleParticipate('no_longer_affected')}
              loading={participation.isPending}
            />
          )}
        </View>

        {/* Add Further Context — only after I'm Affected */}
        {showContext && !contextSubmitted && (
          <View style={styles.contextBlock}>
            <Text style={styles.contextTitle}>Add further context</Text>
            <Text style={styles.contextSub}>
              Optional — describe what you're experiencing (max{' '}
              {CONTEXT_TEXT_MAX_LENGTH} chars).
            </Text>
            <TextInput
              style={styles.contextInput}
              value={contextText}
              onChangeText={(v) =>
                setContextText(v.slice(0, CONTEXT_TEXT_MAX_LENGTH))
              }
              placeholder="e.g. Road completely blocked, cars diverting through side streets…"
              placeholderTextColor="#9ca3af"
              multiline
              numberOfLines={3}
              textAlignVertical="top"
            />
            <Text style={styles.contextCounter}>
              {contextText.length}/{CONTEXT_TEXT_MAX_LENGTH}
            </Text>
            <Button
              label="Submit context"
              onPress={handleSubmitContext}
              loading={addContext.isPending}
              disabled={!contextText.trim()}
            />
          </View>
        )}

        {contextSubmitted && (
          <Text style={styles.contextDone}>Context submitted. Thank you.</Text>
        )}

        {/* Restoration prompt */}
        {isRestoration && (
          <TouchableOpacity
            style={styles.restorationBanner}
            onPress={() =>
              router.push(`/(app)/clusters/${id}/restore`)
            }
          >
            <View>
              <Text style={styles.restorationTitle}>Is this resolved?</Text>
              <Text style={styles.restorationSub}>
                Let us know if service has been restored for you.
              </Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color="#1e40af" />
          </TouchableOpacity>
        )}

        {/* 3 — Official updates */}
        {cluster.officialPosts.length > 0 && (
          <View style={styles.officialSection}>
            <Text style={styles.sectionTitle}>Official updates</Text>
            {cluster.officialPosts.map((post) => (
              <View key={post.id} style={styles.officialCard}>
                <Text style={styles.officialTitle}>{post.title}</Text>
                <Text style={styles.officialBody}>{post.body}</Text>
                <Text style={styles.officialTime}>
                  {formatRelativeTime(post.createdAt)}
                </Text>
              </View>
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

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#f9fafb' },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  navTitle: { fontSize: 16, fontWeight: '600', color: '#111827', flex: 1, textAlign: 'center', marginHorizontal: 8 },
  content: { padding: 16, gap: 16, paddingBottom: 40 },
  titleRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 },
  category: { fontSize: 20, fontWeight: '700', color: '#111827', flex: 1 },
  summary: { fontSize: 15, color: '#374151', lineHeight: 22 },
  actions: { gap: 10 },
  actionsTitle: { fontSize: 15, fontWeight: '600', color: '#374151' },
  actionRow: { flexDirection: 'row', gap: 10 },
  actionBtn: { flex: 1 },
  contextBlock: {
    backgroundColor: '#f0fdf4',
    borderRadius: 12,
    padding: 14,
    gap: 10,
    borderWidth: 1,
    borderColor: '#bbf7d0',
  },
  contextTitle: { fontSize: 16, fontWeight: '600', color: '#166534' },
  contextSub: { fontSize: 13, color: '#166534' },
  contextInput: {
    borderWidth: 1.5,
    borderColor: '#86efac',
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    color: '#111827',
    backgroundColor: '#fff',
    minHeight: 80,
  },
  contextCounter: { fontSize: 12, color: '#6b7280', textAlign: 'right' },
  contextDone: { fontSize: 14, color: '#166534', textAlign: 'center', fontStyle: 'italic' },
  restorationBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#eff6ff',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#bfdbfe',
  },
  restorationTitle: { fontSize: 15, fontWeight: '600', color: '#1e40af' },
  restorationSub: { fontSize: 13, color: '#1e3a8a', marginTop: 2 },
  officialSection: { gap: 10 },
  sectionTitle: { fontSize: 17, fontWeight: '700', color: '#111827' },
  officialCard: {
    backgroundColor: '#fffbeb',
    borderRadius: 10,
    padding: 14,
    gap: 6,
    borderLeftWidth: 3,
    borderLeftColor: '#f59e0b',
  },
  officialTitle: { fontSize: 15, fontWeight: '600', color: '#111827' },
  officialBody: { fontSize: 14, color: '#374151', lineHeight: 20 },
  officialTime: { fontSize: 12, color: '#9ca3af' },
  updated: { fontSize: 12, color: '#9ca3af', textAlign: 'center' },
});
