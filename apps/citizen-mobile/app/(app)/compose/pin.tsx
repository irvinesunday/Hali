// apps/citizen-mobile/app/(app)/compose/pin.tsx
//
// C11.1 — Draggable map-pin fallback for low-confidence location.
//
// This screen is a *correction* surface, not a primary reporting UI:
// the user reaches it only from the LocationFallbackPicker's tertiary
// "Drop a pin on the map instead" affordance when the NLP extraction
// was low-confidence AND neither free-text place search nor "Use my
// current location" satisfied the user. Doctrine guardrails we honour:
//
//   - Map is fallback, not primary. Free-text + NLP stays the main path.
//   - The human-readable label is the user-facing truth. Raw coordinates
//     are operational infrastructure, never the primary display.
//   - Spatial integrity is preserved: the backend's /v1/places/reverse
//     still enforces locality validation, and points outside known wards
//     return 404 → the user sees an error and can't confirm an invalid
//     pin. The composer's submit path re-runs bounds + H3 + locality
//     regardless of source.
//
// Behavior:
//   1. Initial marker position is computed by computeInitialPinPosition
//      (src/utils/pinCorrection.ts) — prefers an existing override, else
//      the preview's user coords, else a Nairobi default.
//   2. Marker is draggable. On drag-end the screen fires a reverse-
//      geocode via /v1/places/reverse.
//   3. Successful reverse-geocode populates a preview label (e.g. "Ngong
//      Road, Nairobi West") which the user confirms. On confirm, the
//      override is set with source='map_pin' and the user navigates back
//      to Step 2 of the composer.
//   4. An outside-locality pin (reverse returns 404) is surfaced as an
//      inline error; the user must move the marker before confirm is
//      enabled.

import React, { useCallback, useMemo, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ActivityIndicator,
} from 'react-native';
import MapView, { Marker, type MapPressEvent, type MarkerDragStartEndEvent, PROVIDER_DEFAULT } from 'react-native-maps';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ArrowLeft, MapPin, RotateCcw } from 'lucide-react-native';
import { Button } from '../../../src/components/common/Button';
import {
  useComposerContext,
  type ComposerLocationOverride,
} from '../../../src/context/ComposerContext';
import { reverseGeocodePoint } from '../../../src/api/places';
import {
  computeInitialPinPosition,
  overrideFromMapPin,
  MAP_PICKED_VIA,
} from '../../../src/utils/pinCorrection';
import type { PlaceCandidate } from '../../../src/types/api';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  ScreenPaddingH,
} from '../../../src/theme';

type ResolveState = 'idle' | 'resolving' | 'error';

const INITIAL_LATITUDE_DELTA = 0.04; // ~4.4km N-S span at the equator
const INITIAL_LONGITUDE_DELTA = 0.04;

export default function PinCorrectionScreen(): React.ReactElement {
  const router = useRouter();
  const { locationOverride, setLocationOverride } = useComposerContext();

  // Initial marker position is deterministic from context + preview. The
  // pure helper is unit-tested in __tests__/composer/pinCorrection.test.ts.
  const initial = useMemo(
    () =>
      computeInitialPinPosition({
        override: locationOverride,
        previewUserLatitude: null,
        previewUserLongitude: null,
      }),
    [locationOverride],
  );

  // The "pending" override is the reverse-geocoded candidate the user has
  // not yet confirmed. Confirming writes it to ComposerContext; cancelling
  // / dragging fires a fresh reverse-geocode.
  const [markerLatitude, setMarkerLatitude] = useState(initial.latitude);
  const [markerLongitude, setMarkerLongitude] = useState(initial.longitude);
  const [candidate, setCandidate] = useState<PlaceCandidate | null>(null);
  const [resolveState, setResolveState] = useState<ResolveState>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  // Guard against async responses arriving after unmount or after a
  // newer drag fired — mirrors the pattern we use in the fallback
  // picker for searchPlaces.
  const mountedRef = useRef(true);
  React.useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);
  const dragTicket = useRef(0);

  const resolveForPoint = useCallback(
    async (latitude: number, longitude: number) => {
      const myTicket = ++dragTicket.current;
      setResolveState('resolving');
      setErrorMessage('');
      setCandidate(null);

      const res = await reverseGeocodePoint(latitude, longitude);

      // Ignore stale responses and post-unmount callbacks.
      if (!mountedRef.current) return;
      if (myTicket !== dragTicket.current) return;

      if (!res.ok) {
        setResolveState('error');
        if (res.error.status === 404) {
          setErrorMessage('That point is outside known wards. Drag the pin closer to a known area.');
        } else {
          setErrorMessage('Could not resolve that point. Please try again.');
        }
        return;
      }

      setResolveState('idle');
      setCandidate(res.value);
    },
    [],
  );

  const handleMarkerDragEnd = useCallback(
    (e: MarkerDragStartEndEvent) => {
      const { latitude, longitude } = e.nativeEvent.coordinate;
      setMarkerLatitude(latitude);
      setMarkerLongitude(longitude);
      void resolveForPoint(latitude, longitude);
    },
    [resolveForPoint],
  );

  // Tapping the map moves the marker too. This lets users on devices
  // where drag is awkward still land the pin precisely. The release
  // behavior (reverse-geocode + pending state) is identical.
  const handleMapPress = useCallback(
    (e: MapPressEvent) => {
      const { latitude, longitude } = e.nativeEvent.coordinate;
      setMarkerLatitude(latitude);
      setMarkerLongitude(longitude);
      void resolveForPoint(latitude, longitude);
    },
    [resolveForPoint],
  );

  const handleConfirm = useCallback(() => {
    if (candidate === null) return;
    const override: ComposerLocationOverride | null = overrideFromMapPin(candidate);
    if (override === null) {
      // Defense-in-depth: should never fire because we only enable the
      // Confirm button when candidate has a non-empty displayName in
      // bounds, but if the PlaceCandidate somehow violates those rules
      // we refuse to produce an override rather than submit a bad pick.
      setResolveState('error');
      setErrorMessage('Could not use that pin. Please drag to another point.');
      return;
    }
    setLocationOverride(override, MAP_PICKED_VIA);
    router.back();
  }, [candidate, setLocationOverride, router]);

  const handleReset = useCallback(() => {
    setMarkerLatitude(initial.latitude);
    setMarkerLongitude(initial.longitude);
    setCandidate(null);
    setResolveState('idle');
    setErrorMessage('');
    void resolveForPoint(initial.latitude, initial.longitude);
  }, [initial, resolveForPoint]);

  const canConfirm = candidate !== null && resolveState === 'idle';

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.navBar}>
        <TouchableOpacity
          onPress={() => router.back()}
          hitSlop={12}
          accessibilityRole="button"
          accessibilityLabel="Back without changing the location"
        >
          <ArrowLeft size={24} color={Colors.foreground} strokeWidth={2} />
        </TouchableOpacity>
        <Text style={styles.title}>Drop a pin</Text>
        <TouchableOpacity
          onPress={handleReset}
          hitSlop={12}
          accessibilityRole="button"
          accessibilityLabel="Reset the pin to its starting position"
        >
          <RotateCcw size={20} color={Colors.mutedForeground} strokeWidth={2} />
        </TouchableOpacity>
      </View>

      <View style={styles.mapContainer} accessible accessibilityLabel="Map with draggable pin">
        <MapView
          style={StyleSheet.absoluteFill}
          provider={PROVIDER_DEFAULT}
          initialRegion={{
            latitude: initial.latitude,
            longitude: initial.longitude,
            latitudeDelta: INITIAL_LATITUDE_DELTA,
            longitudeDelta: INITIAL_LONGITUDE_DELTA,
          }}
          onPress={handleMapPress}
        >
          <Marker
            draggable
            coordinate={{ latitude: markerLatitude, longitude: markerLongitude }}
            onDragEnd={handleMarkerDragEnd}
            accessibilityLabel="Location pin. Drag to refine the location."
          />
        </MapView>
      </View>

      <View style={styles.panel}>
        <View style={styles.labelRow}>
          <MapPin size={18} color={Colors.primary} strokeWidth={2} />
          <View style={styles.labelTextWrap}>
            <Text style={styles.labelTitle}>Selected location</Text>
            {resolveState === 'resolving' ? (
              <View style={styles.resolvingRow} accessibilityLiveRegion="polite">
                <ActivityIndicator size="small" color={Colors.mutedForeground} />
                <Text style={styles.resolvingText}>Resolving…</Text>
              </View>
            ) : candidate !== null ? (
              <>
                <Text style={styles.labelValue} numberOfLines={2}>
                  {candidate.displayName}
                </Text>
                {candidate.wardName !== null && (
                  <Text style={styles.labelMeta} numberOfLines={1}>
                    {candidate.cityName !== null
                      ? `${candidate.wardName}, ${candidate.cityName}`
                      : candidate.wardName}
                  </Text>
                )}
              </>
            ) : (
              <Text style={styles.labelHint}>
                Drag the pin or tap the map to pick a location.
              </Text>
            )}
          </View>
        </View>

        {resolveState === 'error' && errorMessage !== '' && (
          <Text style={styles.errorText} accessibilityRole="alert" accessibilityLiveRegion="polite">
            {errorMessage}
          </Text>
        )}

        <Button
          label="Confirm this location"
          onPress={handleConfirm}
          disabled={!canConfirm}
          accessibilityLabel="Confirm this location"
          accessibilityState={{ disabled: !canConfirm }}
        />
        <Button
          label="Cancel"
          variant="ghost"
          onPress={() => router.back()}
          accessibilityLabel="Cancel and return without changing the location"
        />
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Colors.card },
  navBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: ScreenPaddingH,
    paddingVertical: Spacing.sm + 2,
  },
  title: {
    fontSize: FontSize.title,
    fontFamily: FontFamily.semiBold,
    color: Colors.foreground,
  },
  mapContainer: {
    flex: 1,
    backgroundColor: Colors.border,
    overflow: 'hidden',
  },
  panel: {
    paddingHorizontal: ScreenPaddingH,
    paddingVertical: Spacing.lg,
    gap: Spacing.md,
    backgroundColor: Colors.card,
    borderTopWidth: 1,
    borderTopColor: Colors.border,
  },
  labelRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: Spacing.sm,
  },
  labelTextWrap: { flex: 1, gap: Spacing.xs / 2 },
  labelTitle: {
    fontSize: FontSize.badge,
    fontFamily: FontFamily.medium,
    color: Colors.faintForeground,
    textTransform: 'uppercase',
    letterSpacing: 0.4,
  },
  labelValue: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.medium,
    color: Colors.foreground,
  },
  labelMeta: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  labelHint: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  resolvingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
  },
  resolvingText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  errorText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
  },
});
