// apps/citizen-mobile/app/(app)/compose/confirm.tsx
import React, { useMemo, useState } from 'react';
import {
  View, Text, TextInput, TouchableOpacity, ScrollView,
  StyleSheet, KeyboardAvoidingView, Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft, Check } from 'lucide-react-native';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { Button } from '../../../src/components/common/Button';
import { ConditionBadge } from '../../../src/components/shared';
import { formatCategoryLabel } from '../../../src/utils/formatters';
import { classifyLocationGate, type ConfidenceGate } from '../../../src/utils/composerGates';
import { Colors, FontFamily, FontSize, Spacing, Radius, ScreenPaddingH } from '../../../src/theme';
import type { SignalPreviewResponse } from '../../../src/types/api';

export default function ComposerConfirmScreen(): React.ReactElement {
  const router = useRouter();
  const { preview, setPreview } = useComposerContext();
  if (preview === null) {
    return <PreviewMissingFallback onBack={() => router.replace('/(app)/compose/text')} />;
  }
  return <ConfirmScreenContent preview={preview} setPreview={setPreview} />;
}

function ConfirmScreenContent({
  preview, setPreview,
}: { preview: SignalPreviewResponse; setPreview: (p: SignalPreviewResponse | null) => void }): React.ReactElement {
  const router = useRouter();
  const [locationLabel, setLocationLabel] = useState(preview.location.locationLabel ?? '');
  const [locationConfirmed, setLocationConfirmed] = useState(false);

  const locationGate = useMemo<ConfidenceGate>(
    () => classifyLocationGate(preview.location.locationConfidence),
    [preview.location.locationConfidence],
  );

  const canProceed = useMemo<boolean>(() => {
    const trimmed = locationLabel.trim();
    const original = (preview.location.locationLabel ?? '').trim();
    const userEdited = trimmed !== original && trimmed.length > 0;
    switch (locationGate) {
      case 'accept':   return true;
      case 'confirm':  return locationConfirmed || userEdited;
      case 'required': return trimmed.length > 0;
    }
  }, [locationGate, locationLabel, locationConfirmed, preview.location.locationLabel]);

  function handleNext(): void {
    setPreview({
      ...preview,
      location: { ...preview.location, locationLabel: locationLabel.trim() || preview.location.locationLabel },
    });
    router.push('/(app)/compose/submit');
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity onPress={() => router.back()} hitSlop={12}
          accessibilityRole="button" accessibilityLabel="Back to Step 1">
          <ArrowLeft size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>
        <Text style={styles.stepLabel}>Step 2 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

      <KeyboardAvoidingView style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={80}>
        <ScrollView style={styles.flex} contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled">
          <Text style={styles.heading}>Does this look right?</Text>
          <Text style={styles.sub}>
            Review what we extracted. You can correct the location before submitting.
          </Text>

          <View style={styles.card}>
            <Field label="Category" value={formatCategoryLabel(preview.category)} />
            <Field label="Subcategory" value={formatCategoryLabel(preview.subcategorySlug)} />
            {preview.conditionSlug !== null && preview.conditionSlug !== '' && (
              <View style={styles.fieldRow}>
                <Text style={styles.fieldLabel}>Condition</Text>
                <ConditionBadge label={formatCategoryLabel(preview.conditionSlug)} />
              </View>
            )}
            {preview.neutralSummary !== null && preview.neutralSummary !== '' && (
              <Field label="Summary" value={preview.neutralSummary} />
            )}
          </View>

          <View style={styles.locationBlock}>
            <Text style={styles.fieldLabel}>Location</Text>
            {locationGate === 'required' && (
              <Text style={styles.warningRequired}>
                We couldn't confidently identify the location. Please enter it below.
              </Text>
            )}
            {locationGate === 'confirm' && (
              <Text style={styles.warningAmber}>
                We extracted this location but aren't fully confident. Confirm or correct it.
              </Text>
            )}
            <TextInput
              style={[
                styles.locationInput,
                locationGate === 'required' && locationLabel.trim() === '' && styles.locationInputError,
              ]}
              value={locationLabel}
              onChangeText={(v) => { setLocationLabel(v); setLocationConfirmed(false); }}
              placeholder={locationGate === 'required' ? 'Enter the location…' : 'Confirm or correct the location…'}
              placeholderTextColor={Colors.faintForeground}
              accessibilityLabel="Location"
            />
            {locationGate === 'confirm' && !locationConfirmed && (
              <TouchableOpacity style={styles.confirmChip}
                onPress={() => setLocationConfirmed(true)}
                accessibilityRole="button" accessibilityLabel="Confirm location">
                <Check size={14} color={Colors.primary} strokeWidth={2.5} />
                <Text style={styles.confirmChipText}>Looks right</Text>
              </TouchableOpacity>
            )}
            {locationGate === 'confirm' && locationConfirmed && (
              <Text style={styles.confirmedText}>Location confirmed.</Text>
            )}
          </View>

          <Button label="Next" onPress={handleNext} disabled={!canProceed}
            accessibilityLabel="Next"
            accessibilityState={{ disabled: !canProceed }} />
        </ScrollView>
      </KeyboardAvoidingView>
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
  locationBlock: { gap: Spacing.sm },
  warningRequired: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.destructive },
  warningAmber: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.conditionBadge.amber.text },
  locationInput: {
    borderWidth: 1.5, borderColor: Colors.border, borderRadius: Radius.md,
    paddingHorizontal: Spacing.lg, paddingVertical: Spacing.md,
    fontSize: FontSize.body, fontFamily: FontFamily.regular,
    color: Colors.foreground, backgroundColor: Colors.card,
  },
  locationInputError: { borderColor: Colors.destructive },
  confirmChip: {
    alignSelf: 'flex-start', flexDirection: 'row', alignItems: 'center', gap: Spacing.xs,
    backgroundColor: Colors.primarySubtle, borderRadius: Radius.full,
    paddingVertical: Spacing.xs + 2, paddingHorizontal: Spacing.md,
  },
  confirmChipText: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.medium, color: Colors.primary },
  confirmedText: { fontSize: FontSize.bodySmall, fontFamily: FontFamily.regular, color: Colors.primary, fontStyle: 'italic' },
  missingContainer: { flex: 1, padding: ScreenPaddingH, gap: Spacing.lg, justifyContent: 'center' },
});
