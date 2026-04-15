// apps/citizen-mobile/app/(app)/compose/submit.tsx
import React, { useState } from 'react';
import { View, Text, TouchableOpacity, ScrollView, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft, Info } from 'lucide-react-native';
import * as Crypto from 'expo-crypto';
import * as Location from 'expo-location';
import { submitSignal } from '../../../src/api/signals';
import {
  useComposerContext,
  type ComposerLocationOverride,
} from '../../../src/context/ComposerContext';
import { Button } from '../../../src/components/common/Button';
import { formatCategoryLabel } from '../../../src/utils/formatters';
import { Colors, FontFamily, FontSize, Spacing, Radius, ScreenPaddingH } from '../../../src/theme';
import type {
  SignalLocationSource,
  SignalSubmitRequest,
  SignalPreviewResponse,
} from '../../../src/types/api';
import { ERROR_CODES, isKnownErrorCode } from '../../../src/types/api';

type ScreenState = 'idle' | 'loading' | 'error';

export default function ComposerSubmitScreen(): React.ReactElement {
  const router = useRouter();
  const { freeText, preview, deviceHash, locationOverride, reset } = useComposerContext();
  if (preview === null) {
    return <PreviewMissingFallback onBack={() => router.replace('/(app)/compose/text')} />;
  }
  return (
    <SubmitScreenContent
      freeText={freeText} preview={preview} deviceHash={deviceHash}
      locationOverride={locationOverride}
      onSuccess={(clusterId?: string) => {
        reset();
        if (clusterId) {
          router.replace(`/(app)/clusters/${clusterId}`);
        } else {
          router.replace('/(app)/home');
        }
      }}
    />
  );
}

function SubmitScreenContent({
  freeText, preview, deviceHash, locationOverride, onSuccess,
}: {
  freeText: string;
  preview: SignalPreviewResponse;
  deviceHash: string;
  locationOverride: ComposerLocationOverride | null;
  onSuccess: (clusterId?: string) => void;
}): React.ReactElement {
  const router = useRouter();
  const [screenState, setScreenState] = useState<ScreenState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  async function handleSubmit(): Promise<void> {
    if (screenState === 'loading') return;
    setScreenState('loading');
    setErrorMessage('');

    // Resolve the authoritative submit coordinates + label + source.
    //
    // Path 1 — C11 fallback override: the user picked a PlaceCandidate in
    // the confirm step (or tapped "use my current location"). Both coords
    // and label come from the override and are considered authoritative;
    // we deliberately skip the device-GPS capture here because the
    // override's coordinates are what the user actually chose.
    //
    // Path 2 — default: device GPS provides coords; label + source come
    // from the (possibly user-edited) NLP preview. Source is narrowed
    // through the SignalLocationSource allowlist on the wire.
    let submitLatitude: number;
    let submitLongitude: number;
    let submitLabel: string | undefined;
    let submitSource: SignalLocationSource;

    if (locationOverride !== null) {
      submitLatitude = locationOverride.latitude;
      submitLongitude = locationOverride.longitude;
      submitLabel = locationOverride.label;
      submitSource = locationOverride.source;
    } else {
      const { status } = await Location.requestForegroundPermissionsAsync();
      let position: Location.LocationObject | null = null;
      if (status === 'granted') {
        try {
          position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
        } catch {
          position = await Location.getLastKnownPositionAsync();
        }
      } else {
        position = await Location.getLastKnownPositionAsync();
      }
      if (!position) {
        setScreenState('error');
        setErrorMessage('Could not determine location. Please enable location services and try again.');
        return;
      }
      submitLatitude = position.coords.latitude;
      submitLongitude = position.coords.longitude;
      submitLabel = preview.location.locationLabel ?? undefined;
      // Narrow the preview's raw string to the wire-allowlist union. If NLP
      // ever reports a source we don't recognise we default to 'user_edit'
      // so the backend doesn't reject the submit with
      // validation.invalid_location_source.
      submitSource = narrowLocationSource(preview.location.locationSource);
    }

    const idempotencyKey = await Crypto.digestStringAsync(
      Crypto.CryptoDigestAlgorithm.SHA256, `${freeText}:${Date.now()}`,
    );
    const body: SignalSubmitRequest = {
      idempotencyKey, deviceHash, freeText,
      category: preview.category,
      subcategorySlug: preview.subcategorySlug,
      conditionSlug: preview.conditionSlug ?? undefined,
      conditionConfidence: preview.conditionConfidence,
      latitude: submitLatitude,
      longitude: submitLongitude,
      locationLabel: submitLabel,
      locationPrecisionType: preview.location.locationPrecisionType ?? undefined,
      locationConfidence: preview.location.locationConfidence,
      locationSource: submitSource,
      temporalType: preview.temporalType ?? undefined,
      neutralSummary: preview.neutralSummary ?? undefined,
    };
    const result = await submitSignal(body);
    if (result.ok) { onSuccess(result.value.clusterId); return; }
    setScreenState('error');
    if (result.error.status === 409) { onSuccess(undefined); return; }

    // Discriminable validation messaging — branch on the typed wire code
    // first so the user sees a specific call-to-action for the two
    // composer-affecting validation failures, then fall back to status
    // and finally the server message.
    if (isKnownErrorCode(result.error.code)) {
      switch (result.error.code) {
        case ERROR_CODES.VALIDATION_LOCALITY_UNRESOLVED:
          setErrorMessage('We could not match this report to a ward. Tap back and refine the location.');
          return;
        case ERROR_CODES.VALIDATION_INVALID_CATEGORY:
          setErrorMessage('The selected category is not recognised. Tap back and choose a different one.');
          return;
        case ERROR_CODES.RATE_LIMIT_EXCEEDED:
          setErrorMessage('Too many signals submitted. Please wait a moment and try again.');
          return;
      }
    }
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
          {/* Override wins for display too — if the user picked in the
              fallback picker, show that label instead of the stale NLP one. */}
          {locationOverride !== null ? (
            <Field label="Location" value={locationOverride.label} />
          ) : (
            preview.location.locationLabel !== null && preview.location.locationLabel !== '' && (
              <Field label="Location" value={preview.location.locationLabel} />
            )
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

/**
 * Narrow an arbitrary wire string (typically `preview.location.locationSource`
 * from the backend NLP extraction) to the SignalLocationSource allowlist.
 *
 * The server now normalizes any unknown NLP-emitted value to `'nlp'` in
 * SignalIngestionService.PreviewAsync, so in practice this shim is only
 * exercised when a stale cached preview from an older client version
 * survives. In that case the honest default is `'nlp'` — the preview is
 * NLP-origin data by construction. Defaulting to `'user_edit'` (the
 * prior behaviour) misattributed authorship to the user.
 */
function narrowLocationSource(raw: string): SignalLocationSource {
  if (
    raw === 'nlp' ||
    raw === 'user_edit' ||
    raw === 'place_search' ||
    raw === 'map_pin'
  ) {
    return raw;
  }
  return 'nlp';
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
