// apps/citizen-mobile/app/(app)/compose/submit.tsx
//
// Signal composer — Step 3: submit.
//
// Honest UX note: earlier designs (and the stale OpenAPI) described a
// "join existing or create new" two-button flow. The actual backend
// does NOT accept a joinClusterId — clustering is decided server-side
// during submit processing. We honour the `shouldSuggestJoin` hint by
// showing a warning banner ("similar signals may already exist") but
// still ship a single Submit action. Both paths produce identical API
// calls, so presenting them as a choice would be misleading.
//
// Submit response is { signalEventId, createdAt } — no clusterId.
// Clustering runs asynchronously via a worker. On success we navigate
// to the home feed and let eventual consistency surface the cluster.

import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  ActivityIndicator,
  ScrollView,
  StyleSheet,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as Crypto from 'expo-crypto';
import { submitSignal } from '../../../src/api/signals';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { formatCategoryLabel } from '../../../src/utils/formatters';
import type {
  SignalSubmitRequest,
  SignalPreviewResponse,
} from '../../../src/types/api';

type ScreenState = 'idle' | 'loading' | 'error';

export default function ComposerSubmitScreen(): React.ReactElement {
  const router = useRouter();
  const { freeText, preview, deviceHash, reset } = useComposerContext();

  if (preview === null) {
    return (
      <PreviewMissingFallback
        onBack={() => router.replace('/(app)/compose/text')}
      />
    );
  }

  return (
    <SubmitScreenContent
      freeText={freeText}
      preview={preview}
      deviceHash={deviceHash}
      onSuccess={() => {
        reset();
        router.replace('/(app)/home');
      }}
    />
  );
}

interface SubmitScreenContentProps {
  freeText: string;
  preview: SignalPreviewResponse;
  deviceHash: string;
  onSuccess: () => void;
}

function SubmitScreenContent({
  freeText,
  preview,
  deviceHash,
  onSuccess,
}: SubmitScreenContentProps): React.ReactElement {
  const router = useRouter();
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  async function handleSubmit(): Promise<void> {
    if (screenState === 'loading') return;

    setScreenState('loading');
    setErrorMessage('');

    // Fresh idempotency key per submit attempt. SHA-256 of text+timestamp
    // is effectively unique per submission and stable across retries of
    // the same attempt (the offline queue will replay with the same key).
    const idempotencyKey = await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256,
      `${freeText}:${Date.now()}`,
    );

    const body: SignalSubmitRequest = {
      idempotencyKey,
      deviceHash,
      freeText,
      category: preview.category,
      subcategorySlug: preview.subcategorySlug,
      conditionSlug: preview.conditionSlug ?? undefined,
      conditionConfidence: preview.conditionConfidence,
      locationLabel: preview.location.locationLabel ?? undefined,
      locationPrecisionType:
        preview.location.locationPrecisionType ?? undefined,
      locationConfidence: preview.location.locationConfidence,
      locationSource: preview.location.locationSource,
      temporalType: preview.temporalType ?? undefined,
      neutralSummary: preview.neutralSummary ?? undefined,
    };

    const result = await submitSignal(body);

    if (result.ok) {
      onSuccess();
      return;
    }

    setScreenState('error');
    if (result.error.status === 409) {
      // Idempotency key already used — treat as success from the user's POV.
      onSuccess();
      return;
    }
    if (result.error.status === 429) {
      setErrorMessage(
        'Too many signals submitted. Please wait a moment and try again.',
      );
    } else if (result.error.status === 422) {
      setErrorMessage(
        result.error.message ||
          'Your signal could not be processed. Please review and try again.',
      );
    } else {
      setErrorMessage(
        result.error.message ||
          'Could not submit your signal. Please try again.',
      );
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Back to Step 2"
          disabled={screenState === 'loading'}
        >
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Step 3 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView
        style={styles.flex}
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.heading}>Ready to submit?</Text>
        <Text style={styles.sub}>
          Your signal will be added to your ward&apos;s live feed.
        </Text>

        {/* Summary card mirrors what's about to be sent */}
        <View style={styles.card}>
          <Field
            label="Category"
            value={formatCategoryLabel(preview.category)}
          />
          <Field
            label="Subcategory"
            value={formatCategoryLabel(preview.subcategorySlug)}
          />
          {preview.location.locationLabel !== null &&
            preview.location.locationLabel !== '' && (
              <Field
                label="Location"
                value={preview.location.locationLabel}
              />
            )}
          {preview.neutralSummary !== null && preview.neutralSummary !== '' && (
            <Field label="Summary" value={preview.neutralSummary} />
          )}
        </View>

        {/* shouldSuggestJoin hint — honest advisory, not a choice */}
        {preview.shouldSuggestJoin && (
          <View
            style={styles.hintCard}
            accessible
            accessibilityRole="alert"
          >
            <Ionicons name="information-circle" size={20} color="#1D4ED8" />
            <View style={styles.hintTextWrap}>
              <Text style={styles.hintTitle}>
                Similar signals may already be active
              </Text>
              <Text style={styles.hintBody}>
                We detected nearby signals that sound like yours. You can
                still submit — our system matches duplicates automatically.
              </Text>
            </View>
          </View>
        )}

        {screenState === 'error' && errorMessage !== '' && (
          <Text
            style={styles.error}
            accessible
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
          >
            {errorMessage}
          </Text>
        )}

        <TouchableOpacity
          style={[
            styles.button,
            screenState === 'loading' && styles.buttonDisabled,
          ]}
          onPress={handleSubmit}
          disabled={screenState === 'loading'}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Submit signal"
          accessibilityState={{
            busy: screenState === 'loading',
          }}
        >
          {screenState === 'loading' ? (
            <ActivityIndicator color="#FFFFFF" size="small" />
          ) : (
            <Text style={styles.buttonText}>Submit signal</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.cancelButton}
          onPress={() => router.back()}
          disabled={screenState === 'loading'}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Go back and edit"
        >
          <Text style={styles.cancelButtonText}>Go back and edit</Text>
        </TouchableOpacity>
      </ScrollView>
    </SafeAreaView>
  );
}

function Field({
  label,
  value,
}: {
  label: string;
  value: string;
}): React.ReactElement {
  return (
    <View style={styles.fieldRow}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <Text style={styles.fieldValue}>{value}</Text>
    </View>
  );
}

function PreviewMissingFallback({
  onBack,
}: {
  onBack: () => void;
}): React.ReactElement {
  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.missingContainer}>
        <Text style={styles.heading}>No preview found</Text>
        <Text style={styles.sub}>
          Your composer draft was lost. Please start again.
        </Text>
        <TouchableOpacity
          style={styles.button}
          onPress={onBack}
          accessible
          accessibilityRole="button"
        >
          <Text style={styles.buttonText}>Start over</Text>
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#FFFFFF' },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  navTitle: { fontSize: 14, color: '#6B7280', fontWeight: '500' },
  navSpacer: { width: 24 },
  content: { padding: 20, gap: 16 },
  heading: { fontSize: 22, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6B7280', lineHeight: 22 },
  card: {
    backgroundColor: '#FFFFFF',
    borderRadius: 12,
    padding: 16,
    gap: 12,
    borderWidth: 1,
    borderColor: '#E5E7EB',
  },
  fieldRow: { gap: 2 },
  fieldLabel: { fontSize: 12, color: '#9CA3AF', fontWeight: '500' },
  fieldValue: { fontSize: 15, color: '#111827' },
  hintCard: {
    flexDirection: 'row',
    gap: 10,
    backgroundColor: '#EFF6FF',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: '#BFDBFE',
  },
  hintTextWrap: { flex: 1, gap: 4 },
  hintTitle: { fontSize: 14, fontWeight: '600', color: '#1E3A8A' },
  hintBody: { fontSize: 13, color: '#1E40AF', lineHeight: 19 },
  error: { fontSize: 14, color: '#DC2626' },
  button: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  buttonDisabled: { backgroundColor: '#9CA3AF' },
  buttonText: { color: '#FFFFFF', fontSize: 16, fontWeight: '600' },
  cancelButton: {
    paddingVertical: 14,
    alignItems: 'center',
  },
  cancelButtonText: { fontSize: 15, color: '#6B7280', fontWeight: '500' },
  missingContainer: {
    flex: 1,
    padding: 24,
    gap: 16,
    justifyContent: 'center',
  },
});
