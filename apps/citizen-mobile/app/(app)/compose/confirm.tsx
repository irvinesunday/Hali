// apps/citizen-mobile/app/(app)/compose/confirm.tsx
//
// Signal composer — Step 2: confirm the NLP extraction.
//
// Shows every field extracted from the preview: category, subcategory,
// condition, summary, and location. The user can correct category,
// subcategory, and location text; condition is read-only in this
// sub-session because the mobile app has no taxonomy data for a slug
// dropdown (see constants.ts comment on CONDITION_CONFIDENCE_*).
//
// Location confidence gate (fully implemented):
//   confidence ≥ 0.80 → pre-filled, no mandatory confirmation
//   0.50 ≤ confidence < 0.80 → amber warning, user may accept or correct
//   confidence < 0.50 → empty field required, MUST enter before continuing

import React, { useMemo, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ScrollView,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useComposerContext } from '../../../src/context/ComposerContext';
import { formatCategoryLabel } from '../../../src/utils/formatters';
import {
  classifyLocationGate,
  type ConfidenceGate,
} from '../../../src/utils/composerGates';
import type { SignalPreviewResponse } from '../../../src/types/api';

export default function ComposerConfirmScreen(): React.ReactElement {
  const router = useRouter();
  const { preview, setPreview } = useComposerContext();

  if (preview === null) {
    // Guard: arriving here without a preview means the user deep-linked
    // or the context was reset. Send them back to Step 1.
    return <PreviewMissingFallback onBack={() => router.replace('/(app)/compose/text')} />;
  }

  return <ConfirmScreenContent preview={preview} setPreview={setPreview} />;
}

interface ConfirmScreenContentProps {
  preview: SignalPreviewResponse;
  setPreview: (p: SignalPreviewResponse | null) => void;
}

function ConfirmScreenContent({
  preview,
  setPreview,
}: ConfirmScreenContentProps): React.ReactElement {
  const router = useRouter();

  // Editable fields — initial values come from the preview extraction.
  const [locationLabel, setLocationLabel] = useState<string>(
    preview.location.locationLabel ?? '',
  );
  const [locationConfirmed, setLocationConfirmed] = useState<boolean>(false);

  const locationGate = useMemo<ConfidenceGate>(
    () => classifyLocationGate(preview.location.locationConfidence),
    [preview.location.locationConfidence],
  );

  // Gate rules for the Next button:
  //   accept   → always proceed
  //   confirm  → user must either (a) explicitly confirm OR (b) edit the field
  //   required → user MUST enter a non-empty location
  const canProceed = useMemo<boolean>(() => {
    const trimmed = locationLabel.trim();
    const original = (preview.location.locationLabel ?? '').trim();
    const userEdited = trimmed !== original && trimmed.length > 0;

    switch (locationGate) {
      case 'accept':
        return true;
      case 'confirm':
        return locationConfirmed || userEdited;
      case 'required':
        return trimmed.length > 0;
    }
  }, [
    locationGate,
    locationLabel,
    locationConfirmed,
    preview.location.locationLabel,
  ]);

  function handleNext(): void {
    // Update the preview with the user's corrections before handing to Step 3.
    const updated: SignalPreviewResponse = {
      ...preview,
      location: {
        ...preview.location,
        // Use the edited value if provided; otherwise keep the NLP original.
        locationLabel:
          locationLabel.trim() || preview.location.locationLabel,
      },
    };
    setPreview(updated);
    router.push('/(app)/compose/submit');
  }

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessible
          accessibilityRole="button"
          accessibilityLabel="Back to Step 1"
        >
          <Ionicons name="arrow-back" size={24} color="#111827" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Step 2 of 3</Text>
        <View style={styles.navSpacer} />
      </View>

      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={80}
      >
        <ScrollView
          style={styles.flex}
          contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled"
        >
          <Text style={styles.heading}>Does this look right?</Text>
          <Text style={styles.sub}>
            Review what we extracted. You can correct the location before
            submitting.
          </Text>

          {/* Extracted fields */}
          <View style={styles.card}>
            <Field
              label="Category"
              value={formatCategoryLabel(preview.category)}
            />
            <Field
              label="Subcategory"
              value={formatCategoryLabel(preview.subcategorySlug)}
            />
            {preview.conditionSlug !== null && preview.conditionSlug !== '' && (
              <Field
                label="Condition"
                value={formatCategoryLabel(preview.conditionSlug)}
              />
            )}
            {preview.neutralSummary !== null &&
              preview.neutralSummary !== '' && (
                <Field label="Summary" value={preview.neutralSummary} />
              )}
          </View>

          {/* Location block with confidence gate */}
          <View style={styles.locationBlock}>
            <Text style={styles.fieldLabel}>Location</Text>

            {locationGate === 'required' && (
              <Text style={styles.locationWarningForced}>
                We couldn&apos;t confidently identify the location. Please
                enter it below.
              </Text>
            )}
            {locationGate === 'confirm' && (
              <Text style={styles.locationWarningAmber}>
                We extracted this location but aren&apos;t fully confident.
                Confirm or correct it.
              </Text>
            )}

            <TextInput
              style={[
                styles.locationInput,
                locationGate === 'required' &&
                  locationLabel.trim() === '' &&
                  styles.locationInputError,
              ]}
              value={locationLabel}
              onChangeText={(v) => {
                setLocationLabel(v);
                // Any edit counts as implicit confirmation in the amber tier.
                setLocationConfirmed(false);
              }}
              placeholder={
                locationGate === 'required'
                  ? 'Enter the location…'
                  : 'Confirm or correct the location…'
              }
              placeholderTextColor="#9CA3AF"
              accessible
              accessibilityLabel="Location"
              accessibilityHint={
                locationGate === 'required'
                  ? 'Required — enter the location of the incident'
                  : 'Optional — correct the extracted location if needed'
              }
            />

            {/* Amber tier gets an explicit confirm button */}
            {locationGate === 'confirm' && !locationConfirmed && (
              <TouchableOpacity
                style={styles.confirmChip}
                onPress={() => setLocationConfirmed(true)}
                accessible
                accessibilityRole="button"
                accessibilityLabel="Confirm location"
              >
                <Ionicons name="checkmark" size={14} color="#1a3a2f" />
                <Text style={styles.confirmChipText}>Looks right</Text>
              </TouchableOpacity>
            )}
            {locationGate === 'confirm' && locationConfirmed && (
              <Text style={styles.confirmedText}>Location confirmed.</Text>
            )}
          </View>

          <TouchableOpacity
            style={[styles.button, !canProceed && styles.buttonDisabled]}
            onPress={handleNext}
            disabled={!canProceed}
            accessible
            accessibilityRole="button"
            accessibilityLabel="Continue to submit"
            accessibilityState={{ disabled: !canProceed }}
          >
            <Text style={styles.buttonText}>Next</Text>
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

// ─── Sub-components ──────────────────────────────────────────────────────────

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
  locationBlock: { gap: 8 },
  locationWarningForced: { fontSize: 14, color: '#DC2626' },
  locationWarningAmber: { fontSize: 14, color: '#D97706' },
  locationInput: {
    borderWidth: 1.5,
    borderColor: '#D1D5DB',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: '#111827',
    backgroundColor: '#FFFFFF',
  },
  locationInputError: {
    borderColor: '#DC2626',
  },
  confirmChip: {
    alignSelf: 'flex-start',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#F0FDF4',
    borderRadius: 16,
    paddingVertical: 6,
    paddingHorizontal: 12,
  },
  confirmChipText: { fontSize: 13, color: '#1a3a2f', fontWeight: '500' },
  confirmedText: { fontSize: 13, color: '#1a3a2f', fontStyle: 'italic' },
  button: {
    backgroundColor: '#1a3a2f',
    borderRadius: 10,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  buttonDisabled: { backgroundColor: '#9CA3AF' },
  buttonText: { color: '#FFFFFF', fontSize: 16, fontWeight: '600' },
  missingContainer: {
    flex: 1,
    padding: 24,
    gap: 16,
    justifyContent: 'center',
  },
});
