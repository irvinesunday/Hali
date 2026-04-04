import React from 'react';
import { View, Text, ScrollView, StyleSheet } from 'react-native';
import { Button } from '../common/Button';
import type { SignalPreviewResponse } from '../../types/api';

// NOTE(API-MISMATCH): The OpenAPI spec includes existingClusterCandidates[] in
// the preview response, but the actual backend SignalPreviewResponseDto only
// exposes shouldSuggestJoin: boolean. When shouldSuggestJoin is true we offer a
// "join" path; the backend resolves the actual join internally during submit.
// Track: no open issue — backend and spec diverge here.

interface ComposerStep3Props {
  preview: SignalPreviewResponse;
  onSubmit: (joinExisting: boolean) => void;
  loading: boolean;
}

export function ComposerStep3({ preview, onSubmit, loading }: ComposerStep3Props) {
  return (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={styles.content}
    >
      <Text style={styles.heading}>Ready to report?</Text>
      <Text style={styles.sub}>
        Choose how to file this signal.
      </Text>

      {preview.shouldSuggestJoin && (
        <View style={styles.joinCard}>
          <Text style={styles.joinTitle}>Similar report exists nearby</Text>
          <Text style={styles.joinBody}>
            There appears to be an active signal similar to yours. You can join
            it to add your voice, or report separately if yours is different.
          </Text>
          <Button
            label="Join this signal"
            onPress={() => onSubmit(true)}
            loading={loading}
            style={styles.joinBtn}
          />
        </View>
      )}

      <View style={styles.newCard}>
        <Text style={styles.newTitle}>Report as new signal</Text>
        <Text style={styles.newBody}>
          File this as a separate, independent civic signal.
        </Text>
        <Button
          label="Report as new signal"
          variant={preview.shouldSuggestJoin ? 'secondary' : 'primary'}
          onPress={() => onSubmit(false)}
          loading={loading && !preview.shouldSuggestJoin}
        />
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 20, gap: 16 },
  heading: { fontSize: 22, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  joinCard: {
    backgroundColor: '#eff6ff',
    borderRadius: 12,
    padding: 16,
    gap: 10,
    borderWidth: 1,
    borderColor: '#bfdbfe',
  },
  joinTitle: { fontSize: 16, fontWeight: '600', color: '#1e40af' },
  joinBody: { fontSize: 14, color: '#1e3a8a', lineHeight: 20 },
  joinBtn: {},
  newCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    gap: 10,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  newTitle: { fontSize: 16, fontWeight: '600', color: '#111827' },
  newBody: { fontSize: 14, color: '#6b7280', lineHeight: 20 },
});
