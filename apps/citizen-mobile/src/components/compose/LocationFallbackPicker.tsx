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
import { MapPin, Navigation2, Search, X } from 'lucide-react-native';
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

export interface LocationFallbackPickerProps {
  /**
   * The picked override, if any. Rendering the "selected" state when
   * non-null so the user sees their choice and can change it.
   */
  selected: ComposerLocationOverride | null;
  onPick: (override: ComposerLocationOverride) => void;
  onClear: () => void;
}

const SEARCH_DEBOUNCE_MS = 400;
const MIN_QUERY_LENGTH = 2;

export function LocationFallbackPicker({
  selected,
  onPick,
  onClear,
}: LocationFallbackPickerProps): React.ReactElement {
  const [query, setQuery] = useState('');
  const [candidates, setCandidates] = useState<PlaceCandidate[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [resolvingGps, setResolvingGps] = useState(false);
  const [gpsError, setGpsError] = useState<string | null>(null);

  // Debounced search. Every effect registration schedules the search and
  // clears the previous pending timer; the cleanup also cancels in-flight
  // requests when a newer query fires. This keeps the list consistent with
  // whatever the user last typed and prevents ghost results after unmount.
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
      // Stale response guard: a newer query has started, drop this one.
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
      onPick({
        latitude: c.latitude,
        longitude: c.longitude,
        label: c.displayName,
        source: 'place_search',
      });
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
      if (!point) {
        setGpsError('Could not determine your current location.');
        return;
      }
      const reverse = await reverseGeocodePoint(
        point.coords.latitude,
        point.coords.longitude,
      );
      if (!reverse.ok) {
        if (reverse.error.status === 404) {
          setGpsError('Your current location is outside known wards.');
        } else {
          setGpsError('Could not resolve your current location.');
        }
        return;
      }
      onPick({
        latitude: reverse.value.latitude,
        longitude: reverse.value.longitude,
        label: reverse.value.displayName,
        source: 'place_search',
      });
    } finally {
      setResolvingGps(false);
    }
  }, [onPick, resolvingGps]);

  if (selected !== null) {
    return (
      <View style={styles.selectedCard} accessibilityLiveRegion="polite">
        <MapPin size={18} color={Colors.primary} strokeWidth={2} />
        <View style={styles.selectedTextWrap}>
          <Text style={styles.selectedLabel}>{selected.label}</Text>
          <Text style={styles.selectedMeta}>
            {selected.source === 'place_search'
              ? 'Selected from place search'
              : 'Current location'}
          </Text>
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
