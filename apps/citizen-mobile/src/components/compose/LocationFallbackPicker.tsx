// apps/citizen-mobile/src/components/compose/LocationFallbackPicker.tsx
//
// C11 low-confidence location fallback UX.
//
// Rendered inside the composer's confirm screen (Step 2) when the server
// flags the NLP extraction as low-confidence. Offers:
//   1. Debounced place search (/v1/places/search) — up to 5 Hali-resolved
//      candidates with ward context.
//   2. "Use my current location" (expo-location + /v1/places/reverse) —
//      one-tap capture of device GPS resolved to a civic label.
//
// Selecting either path produces a ComposerLocationOverride with
// authoritative latitude / longitude / label / source. The component does
// not itself call any composer-context setters — it reports the pick via
// `onPick` so the parent screen can wire it into its own state machine
// and keep the picker stateless from the context's perspective.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { Map as MapIcon, MapPin, Navigation2, Search, X } from 'lucide-react-native';
import * as Location from 'expo-location';
import { searchPlaces, reverseGeocodePoint } from '../../api/places';
import type {
  ComposerLocationOverride,
} from '../../context/ComposerContext';
import type { PlaceCandidate } from '../../types/api';
import {
  Colors,
  FontFamily,
  FontSize,
  Spacing,
  Radius,
} from '../../theme';

/**
 * UI-only record of which picker path produced the selected override.
 *
 * For 'search' and 'current' the wire `locationSource` is `'place_search'`
 * (the label comes from the same backend geocoding service either way).
 * For 'map' the wire `locationSource` is `'map_pin'` (C11.1) because the
 * coordinates came from a user marker, not a Nominatim lookup.
 *
 * pickedVia drives only the selected-state subtitle copy and must not
 * leak back onto the wire.
 */
export type PickerPath = 'search' | 'current' | 'map';

export interface LocationFallbackPickerProps {
  /**
   * The picked override, if any. Rendering the "selected" state when
   * non-null so the user sees their choice and can change it.
   */
  selected: ComposerLocationOverride | null;
  /**
   * The UI-only path that produced {@link selected}. Controls the
   * selected-state subtitle copy. Null when `selected` is null.
   */
  pickedVia: PickerPath | null;
  onPick: (override: ComposerLocationOverride, pickedVia: PickerPath) => void;
  onClear: () => void;
  /**
   * Navigate to the C11.1 draggable map-pin sub-screen. The picker itself
   * stays unaware of routing — the parent composer screen owns navigation
   * and feeds the resulting override back via onPick when the user
   * confirms the pin.
   */
  onOpenMapPin: () => void;
}

const SEARCH_DEBOUNCE_MS = 400;
const MIN_QUERY_LENGTH = 2;

export function LocationFallbackPicker({
  selected,
  pickedVia,
  onPick,
  onClear,
  onOpenMapPin,
}: LocationFallbackPickerProps): React.ReactElement {
  const [query, setQuery] = useState('');
  const [candidates, setCandidates] = useState<PlaceCandidate[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [resolvingGps, setResolvingGps] = useState(false);
  const [gpsError, setGpsError] = useState<string | null>(null);

  // Unmount guard: searchPlaces / reverseGeocodePoint / expo-location
  // calls can resolve after the picker is unmounted (component swapped
  // out by the parent when the gate changes, or the user navigates
  // back). Gating every async setState through this ref prevents
  // "Can't perform a React state update on an unmounted component"
  // warnings and ghost UI updates. The search-ticket counter below still
  // handles stale responses *between* two different queries while the
  // component is mounted.
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // Debounced search. Every effect registration schedules the search and
  // clears the previous pending timer; a newer query's response wins
  // thanks to the ticket counter.
  const searchCounter = useRef(0);
  useEffect(() => {
    const trimmed = query.trim();
    if (trimmed.length < MIN_QUERY_LENGTH) {
      setCandidates([]);
      setSearching(false);
      setSearchError(null);
      return;
    }

    const myTicket = ++searchCounter.current;
    setSearching(true);
    setSearchError(null);

    const handle = setTimeout(async () => {
      const res = await searchPlaces(trimmed);
      // Drop if: component unmounted OR a newer query has started.
      if (!mountedRef.current) return;
      if (myTicket !== searchCounter.current) return;
      setSearching(false);
      if (res.ok) {
        setCandidates(res.value);
      } else {
        setCandidates([]);
        setSearchError('Search is temporarily unavailable.');
      }
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(handle);
  }, [query]);

  const handlePickCandidate = useCallback(
    (c: PlaceCandidate) => {
      onPick(
        {
          latitude: c.latitude,
          longitude: c.longitude,
          label: c.displayName,
          source: 'place_search',
        },
        'search',
      );
      setQuery('');
      setCandidates([]);
    },
    [onPick],
  );

  const handleUseCurrentLocation = useCallback(async () => {
    if (resolvingGps) return;
    setResolvingGps(true);
    setGpsError(null);
    try {
      const perm = await Location.requestForegroundPermissionsAsync();
      if (!mountedRef.current) return;
      if (perm.status !== 'granted') {
        setGpsError('Location permission is required to use your current location.');
        return;
      }
      let point: Location.LocationObject | null = null;
      try {
        point = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        });
      } catch {
        point = await Location.getLastKnownPositionAsync();
      }
      if (!mountedRef.current) return;
      if (!point) {
        setGpsError('Could not determine your current location.');
        return;
      }
      const reverse = await reverseGeocodePoint(
        point.coords.latitude,
        point.coords.longitude,
      );
      if (!mountedRef.current) return;
      if (!reverse.ok) {
        if (reverse.error.status === 404) {
          setGpsError('Your current location is outside known wards.');
        } else {
          setGpsError('Could not resolve your current location.');
        }
        return;
      }
      onPick(
        {
          latitude: reverse.value.latitude,
          longitude: reverse.value.longitude,
          label: reverse.value.displayName,
          source: 'place_search',
        },
        'current',
      );
    } finally {
      if (mountedRef.current) setResolvingGps(false);
    }
  }, [onPick, resolvingGps]);

  if (selected !== null) {
    // Subtitle copy is driven by the UI-only pickedVia flag. pickedVia
    // can be null when the parent restored a selection without an
    // explicit path (e.g. after a remount) — in that case we fall back
    // to the neutral "place search" copy for wire source='place_search'
    // and to "Dropped pin" for wire source='map_pin' (the map path is
    // the only way to produce source='map_pin' today).
    const subtitle =
      pickedVia === 'map' || selected.source === 'map_pin'
        ? 'Dropped pin on map'
        : pickedVia === 'current'
        ? 'Current location'
        : 'Selected from place search';
    return (
      <View style={styles.selectedCard} accessibilityLiveRegion="polite">
        <MapPin size={18} color={Colors.primary} strokeWidth={2} />
        <View style={styles.selectedTextWrap}>
          <Text style={styles.selectedLabel}>{selected.label}</Text>
          <Text style={styles.selectedMeta}>{subtitle}</Text>
        </View>
        <TouchableOpacity
          onPress={onClear}
          hitSlop={12}
          accessibilityRole="button"
          accessibilityLabel="Change location"
        >
          <X size={18} color={Colors.mutedForeground} strokeWidth={2} />
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.hint} accessibilityRole="alert">
        We couldn&apos;t confidently identify the location. Help us confirm
        where this is.
      </Text>

      <View style={styles.searchRow}>
        <Search size={18} color={Colors.mutedForeground} strokeWidth={2} />
        <TextInput
          style={styles.searchInput}
          value={query}
          onChangeText={setQuery}
          placeholder="Search for a place or landmark"
          placeholderTextColor={Colors.faintForeground}
          autoCapitalize="none"
          autoCorrect={false}
          accessibilityLabel="Search for a place"
          accessibilityHint="Enter a road, landmark, or area name to see matching places"
        />
        {searching && (
          <ActivityIndicator size="small" color={Colors.mutedForeground} />
        )}
      </View>

      {searchError !== null && (
        <Text style={styles.errorText} accessibilityRole="alert">
          {searchError}
        </Text>
      )}

      {candidates.length > 0 && (
        <View style={styles.resultList} accessible accessibilityLabel="Place search results">
          {candidates.map((c) => (
            <TouchableOpacity
              // Nominatim does not always return stable IDs; the
              // (lat,lng,label) triple is unique enough for a 5-row list.
              key={`${c.latitude}:${c.longitude}:${c.displayName}`}
              style={styles.resultRow}
              onPress={() => handlePickCandidate(c)}
              accessibilityRole="button"
              accessibilityLabel={`Select ${c.displayName}`}
            >
              <MapPin size={16} color={Colors.mutedForeground} strokeWidth={2} />
              <View style={styles.resultTextWrap}>
                <Text style={styles.resultTitle} numberOfLines={1}>
                  {c.displayName}
                </Text>
                {c.wardName !== null && (
                  <Text style={styles.resultMeta} numberOfLines={1}>
                    {c.cityName !== null
                      ? `${c.wardName}, ${c.cityName}`
                      : c.wardName}
                  </Text>
                )}
              </View>
            </TouchableOpacity>
          ))}
        </View>
      )}

      <TouchableOpacity
        style={styles.gpsButton}
        onPress={handleUseCurrentLocation}
        disabled={resolvingGps}
        accessibilityRole="button"
        accessibilityLabel="Use my current location"
        accessibilityState={{ disabled: resolvingGps, busy: resolvingGps }}
      >
        {resolvingGps ? (
          <ActivityIndicator size="small" color={Colors.primary} />
        ) : (
          <Navigation2 size={16} color={Colors.primary} strokeWidth={2} />
        )}
        <Text style={styles.gpsButtonText}>Use my current location</Text>
      </TouchableOpacity>

      {gpsError !== null && (
        <Text style={styles.errorText} accessibilityRole="alert">
          {gpsError}
        </Text>
      )}

      {/*
        C11.1: draggable map-pin fallback.
        Rendered as a tertiary affordance beneath search + current location
        so the composer continues to read label-first: search is primary,
        current location is secondary, the map is a deliberate escape
        hatch when neither produced what the user needs. Do not promote
        this above the text input / GPS button — that would tilt the
        composer toward a map-first reporting surface, which Hali's
        doctrine explicitly disallows.
      */}
      <TouchableOpacity
        style={styles.mapLink}
        onPress={onOpenMapPin}
        accessibilityRole="button"
        accessibilityLabel="Drop a pin on the map"
        accessibilityHint="Open a map where you can drag a marker to your location"
      >
        <MapIcon size={14} color={Colors.mutedForeground} strokeWidth={2} />
        <Text style={styles.mapLinkText}>Drop a pin on the map instead</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: Spacing.sm },
  hint: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
  },
  searchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    borderWidth: 1.5,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    paddingHorizontal: Spacing.md,
    backgroundColor: Colors.card,
  },
  searchInput: {
    flex: 1,
    paddingVertical: Spacing.md,
    fontSize: FontSize.body,
    fontFamily: FontFamily.regular,
    color: Colors.foreground,
  },
  resultList: {
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    overflow: 'hidden',
    backgroundColor: Colors.card,
  },
  resultRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: Colors.border,
  },
  resultTextWrap: { flex: 1, gap: 2 },
  resultTitle: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.medium,
    color: Colors.foreground,
  },
  resultMeta: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
  gpsButton: {
    alignSelf: 'flex-start',
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    backgroundColor: Colors.primarySubtle,
    borderRadius: Radius.full,
    paddingVertical: Spacing.sm,
    paddingHorizontal: Spacing.md,
  },
  gpsButtonText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.medium,
    color: Colors.primary,
  },
  mapLink: {
    alignSelf: 'flex-start',
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
    paddingVertical: Spacing.xs,
    // Intentionally unstyled / text-link shaped — tertiary affordance,
    // not a third peer button. Keeps the map feel like an escape hatch.
  },
  mapLinkText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
    textDecorationLine: 'underline',
  },
  errorText: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.destructive,
  },
  selectedCard: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: Radius.md,
    padding: Spacing.md,
    backgroundColor: Colors.primarySubtle,
  },
  selectedTextWrap: { flex: 1, gap: 2 },
  selectedLabel: {
    fontSize: FontSize.body,
    fontFamily: FontFamily.medium,
    color: Colors.foreground,
  },
  selectedMeta: {
    fontSize: FontSize.bodySmall,
    fontFamily: FontFamily.regular,
    color: Colors.mutedForeground,
  },
});
