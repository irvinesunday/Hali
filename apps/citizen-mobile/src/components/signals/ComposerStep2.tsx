import React, { useState } from 'react';
import { View, Text, ScrollView, StyleSheet } from 'react-native';
import { Button } from '../common/Button';
import { TextInput } from '../common/TextInput';
import { formatCategoryLabel } from '../../utils/formatters';
import {
  LOCATION_CONFIDENCE_WARN_THRESHOLD,
  LOCATION_CONFIDENCE_AMBER_THRESHOLD,
} from '../../config/constants';
import type { SignalPreviewResponse } from '../../types/api';

interface ComposerStep2Props {
  preview: SignalPreviewResponse;
  onConfirm: (updated: SignalPreviewResponse) => void;
  onEdit: () => void;
}

export function ComposerStep2({ preview, onConfirm, onEdit }: ComposerStep2Props) {
  const [category, setCategory] = useState(preview.category);
  const [subcategorySlug, setSubcategorySlug] = useState(preview.subcategorySlug);
  const [locationLabel, setLocationLabel] = useState(
    preview.location.locationLabel ?? '',
  );

  const conf = preview.location.locationConfidence;
  const locationForced = conf < LOCATION_CONFIDENCE_WARN_THRESHOLD;
  const locationAmber =
    conf >= LOCATION_CONFIDENCE_WARN_THRESHOLD &&
    conf < LOCATION_CONFIDENCE_AMBER_THRESHOLD;

  const canProceed = !locationForced || locationLabel.trim().length > 0;

  function handleConfirm() {
    onConfirm({
      ...preview,
      category,
      subcategorySlug,
      location: {
        ...preview.location,
        locationLabel: locationLabel.trim() || preview.location.locationLabel,
      },
    });
  }

  return (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
    >
      <Text style={styles.heading}>Does this look right?</Text>
      <Text style={styles.sub}>
        Review what we extracted. You can correct any field before submitting.
      </Text>

      <View style={styles.card}>
        <Field label="Category" value={formatCategoryLabel(category)} />
        <Field label="Subcategory" value={formatCategoryLabel(subcategorySlug)} />
        {preview.conditionSlug ? (
          <Field
            label="Condition"
            value={formatCategoryLabel(preview.conditionSlug)}
          />
        ) : null}
        {preview.neutralSummary ? (
          <Field label="Summary" value={preview.neutralSummary} />
        ) : null}
      </View>

      {/* Location block */}
      <View style={styles.locationBlock}>
        <Text style={styles.fieldLabel}>Location</Text>

        {locationForced ? (
          <Text style={styles.locationWarningForced}>
            We couldn't confidently identify the location. Please enter it below.
          </Text>
        ) : locationAmber ? (
          <Text style={styles.locationWarningAmber}>
            We're not fully confident in this location. You can correct it.
          </Text>
        ) : null}

        <TextInput
          value={locationLabel}
          onChangeText={setLocationLabel}
          placeholder="Enter or confirm location…"
          error={
            locationForced && !locationLabel.trim()
              ? 'Location is required'
              : undefined
          }
        />
      </View>

      <View style={styles.actions}>
        <Button
          label="Looks right"
          onPress={handleConfirm}
          disabled={!canProceed}
        />
        <Button label="Edit details" variant="secondary" onPress={onEdit} />
      </View>
    </ScrollView>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.fieldRow}>
      <Text style={styles.fieldLabel}>{label}</Text>
      <Text style={styles.fieldValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 20, gap: 16 },
  heading: { fontSize: 22, fontWeight: '700', color: '#111827' },
  sub: { fontSize: 15, color: '#6b7280', lineHeight: 22 },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    gap: 12,
    borderWidth: 1,
    borderColor: '#e5e7eb',
  },
  fieldRow: { gap: 2 },
  fieldLabel: { fontSize: 12, color: '#9ca3af', fontWeight: '500' },
  fieldValue: { fontSize: 15, color: '#111827' },
  locationBlock: { gap: 8 },
  locationWarningForced: { fontSize: 14, color: '#dc2626' },
  locationWarningAmber: { fontSize: 14, color: '#d97706' },
  actions: { gap: 10, marginTop: 8 },
});
