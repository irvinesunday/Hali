// apps/citizen-mobile/app/(app)/compose/submit.tsx
import React, { useState } from 'react';
import { View, Text, TouchableOpacity, ScrollView, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft, Info } from 'lucide-react-native';
import * as Crypto from 'expo-crypto';
import { submitSignal } from '../../../src/api/signals';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { Button } from '../../../src/components/common/Button';
import { formatCategoryLabel } from '../../../src/utils/formatters';
import { Colors, FontFamily, FontSize, Spacing, Radius, ScreenPaddingH } from '../../../src/theme';
import type { SignalSubmitRequest, SignalPreviewResponse } from '../../../src/types/api';

type ScreenState = 'idle' | 'loading' | 'error';

export default function ComposerSubmitScreen(): React.ReactElement {
  const router = useRouter();
  const { freeText, preview, deviceHash, reset } = useComposerContext();
  if (preview === null) {
    return <PreviewMissingFallback onBack={() => router.replace('/(app)/compose/text')} />;
  }
  return (
    <SubmitScreenContent
      freeText={freeText} preview={preview} deviceHash={deviceHash}
      onSuccess={() => { reset(); router.replace('/(app)/home'); }}
    />
  );
}

function SubmitScreenContent({
  freeText, preview, deviceHash, onSuccess,
}: { freeText: string; preview: SignalPreviewResponse; deviceHash: string; onSuccess: () => void }): React.ReactElement {
  const router = useRouter();
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  async function handleSubmit(): Promise<void> {
    if (screenState === 'loading') return;
    setScreenState('loading');
    setErrorMessage('');
    const idempotencyKey = await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256, `${freeText}:${Date.now()}`,
    );
    const body: SignalSubmitRequest = {
      idempotencyKey, deviceHash, freeText,
      category: preview.category,
      subcategorySlug: preview.subcategorySlug,
      conditionSlug: preview.conditionSlug ?? undefined,
      conditionConfidence: preview.conditionConfidence,
      locationLabel: preview.location.locationLabel ?? undefined,
      locationPrecisionType: preview.location.locationPrecisionType ?? undefined,
      locationConfidence: preview.location.locationConfidence,
      locationSource: preview.location.locationSource,
      temporalType: preview.temporalType ?? undefined,
      neutralSummary: preview.neutralSummary ?? undefined,
    };
    const result = await submitSignal(body);
    if (result.ok) { onSuccess(); return; }
    setScreenState('error');
    if (result.error.status === 409) { onSuccess(); return; }
    if (result.error.status === 429) {
      setErrorMessage('Too many signals submitted. Please wait a moment and try again.');
    } else if (result.error.status === 422) {
      setErrorMessage(result.error.message || 'Your signal could not be processed. Please review and try again.');
    } else {
      setErrorMessage(result.error.message || 'Could not submit your signal. Please try again.');
    }
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}
          accessibilityRole="button" accessibilityLabel="Back to Step 2"
          disabled={screenState === 'loading'}>
          <ArrowLeft size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>
        <Text style={styles.stepLabel}>Step 3 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

      <ScrollView style={styles.flex} contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled">
        <Text style={styles.heading}>Ready to submit?</Text>
        <Text style={styles.sub}>Your signal will be added to your ward's live feed.</Text>

        <View style={styles.card}>
          <Field label="Category" value={formatCategoryLabel(preview.category)} />
          <Field label="Subcategory" value={formatCategoryLabel(preview.subcategorySlug)} />
          {preview.location.locationLabel !== null && preview.location.locationLabel !== '' && (
            <Field label="Location" value={preview.location.locationLabel} />
          )}
          {preview.neutralSummary !== null && preview.neutralSummary !== '' && (
            <Field label="Summary" value={preview.neutralSummary} />
          )}
        </View>

        {preview.shouldSuggestJoin && (
          <View style={styles.hintCard} accessible accessibilityRole="alert">
            <Info size={20} color={Colors.primary} strokeWidth={2} />
            <View style={styles.hintTextWrap}>
              <Text style={styles.hintTitle}>Similar signals may already be active</Text>
              <Text style={styles.hintBody}>
                We detected nearby signals that sound like yours. You can still submit —
                our system matches duplicates automatically.
              </Text>
            </View>
          </View>
        )}

        {screenState === 'error' && errorMessage !== '' && (
          <Text style={styles.error} accessibilityRole="alert" accessibilityLiveRegion="polite">
            {errorMessage}
          </Text>
        )}

        <Button label="Submit signal" onPress={handleSubmit}
          loading={screenState === 'loading'} disabled={screenState === 'loading'}
          accessibilityLabel="Submit signal"
          accessibilityState={{ disabled: screenState === 'loading', busy: screenState === 'loading' }} />
        <Button label="Go back and edit" variant="ghost"
          onPress={() => router.back()} disabled={screenState === 'loading'} />
      </ScrollView>
    </SafeAreaView>
  );
}

function Field({ label, value }: { label: string; value: string }): React.ReactElement {
  return (
    <View style={styles.fieldRow}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <Text style={styles.fieldValue}>{value}</Text>
    </View>
  );
}

function PreviewMissingFallback({ onBack }: { onBack: () => void }): React.ReactElement {
  return (
    <SafeAreaView style={styles.safe}>
      <View style={styles.missingContainer}>
        <Text style={styles.heading}>No preview found</Text>
        <Text style={styles.sub}>Your composer draft was lost. Please start again.</Text>
        <Button label="Start over" onPress={onBack} />
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  flex: { flex: 1 },
  navBar: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingHorizontal: ScreenPaddingH, paddingVertical: Spacing.sm + 2,
  },
  stepLabel: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.medium, color: Colors.mutedForeground },
  navSpacer: { width: 24 },
  content: { paddingHorizontal: ScreenPaddingH, paddingTop: Spacing.lg, paddingBottom: Spacing['4xl'], gap: Spacing.lg },
  heading: { fontSize: FontSize.title, fontFamily: FontFamily.bold, color: Colors.foreground },
  sub: { fontSize: FontSize.body, fontFamily: FontFamily.regular, color: Colors.mutedForeground, lineHeight: FontSize.body * 1.5 },
  card: {
    backgroundColor: Colors.card, borderRadius: Radius.lg, padding: Spacing.lg,
    gap: Spacing.md, borderWidth: 1, borderColor: Colors.border,
  },
  fieldRow: { gap: Spacing.xs },
  fieldLabel: {
    fontSize: FontSize.badge, fontFamily: FontFamily.medium, color: Colors.faintForeground,
    textTransform: 'uppercase', letterSpacing: 0.4,
  },
  fieldValue: { fontSize: FontSize.body, fontFamily: FontFamily.regular, color: Colors.foreground },
  hintCard: {
    flexDirection: 'row', gap: Spacing.md, backgroundColor: Colors.primarySubtle,
    borderRadius: Radius.lg, padding: Spacing.lg, borderWidth: 1,
    borderColor: Colors.primary + '30',
  },
  hintTextWrap: { flex: 1, gap: Spacing.xs },
  hintTitle: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.semiBold, color: Colors.primary },
  hintBody: {
    fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular,
    color: Colors.primary, lineHeight: FontSize.bodySmall * 1.4, opacity: 0.85,
  },
  error: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.destructive },
  missingContainer: { flex: 1, padding: ScreenPaddingH, gap: Spacing.lg, justifyContent: 'center' },
});
