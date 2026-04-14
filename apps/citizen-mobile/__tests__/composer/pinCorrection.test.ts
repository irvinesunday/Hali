// apps/citizen-mobile/__tests__/composer/pinCorrection.test.ts
//
// Pure-helper tests for the C11.1 draggable map-pin fallback.
// No React Native, no react-native-maps — just the selection logic.

import {
  DEFAULT_PIN_LATITUDE,
  DEFAULT_PIN_LONGITUDE,
  computeInitialPinPosition,
  overrideFromMapPin,
  MAP_PICKED_VIA,
} from '../../src/utils/pinCorrection';
import type { ComposerLocationOverride } from '../../src/context/ComposerContext';
import type { PlaceCandidate } from '../../src/types/api';

describe('computeInitialPinPosition', () => {
  it('prefers an existing override when one is present (refinement use-case)', () => {
    const override: ComposerLocationOverride = {
      latitude: -1.303,
      longitude: 36.79,
      label: 'Ngong Road, Nairobi West',
      source: 'place_search',
    };
    const pos = computeInitialPinPosition({
      override,
      previewUserLatitude: -1.5,
      previewUserLongitude: 37.0,
    });
    expect(pos).toEqual({ latitude: -1.303, longitude: 36.79 });
  });

  it('falls back to the preview user lat/lng when no override is set', () => {
    const pos = computeInitialPinPosition({
      override: null,
      previewUserLatitude: -1.28,
      previewUserLongitude: 36.82,
    });
    expect(pos).toEqual({ latitude: -1.28, longitude: 36.82 });
  });

  it('falls back to the Nairobi default when neither override nor preview coords are available', () => {
    const pos = computeInitialPinPosition({
      override: null,
      previewUserLatitude: null,
      previewUserLongitude: null,
    });
    expect(pos).toEqual({
      latitude: DEFAULT_PIN_LATITUDE,
      longitude: DEFAULT_PIN_LONGITUDE,
    });
  });

  it('rejects an out-of-bounds override and falls back to preview coords', () => {
    const badOverride: ComposerLocationOverride = {
      latitude: 95, // > 90
      longitude: 36.8,
      label: 'garbage',
      source: 'place_search',
    };
    const pos = computeInitialPinPosition({
      override: badOverride,
      previewUserLatitude: -1.28,
      previewUserLongitude: 36.82,
    });
    expect(pos).toEqual({ latitude: -1.28, longitude: 36.82 });
  });

  it('rejects non-finite preview coords and falls back to the default', () => {
    const pos = computeInitialPinPosition({
      override: null,
      previewUserLatitude: Number.NaN,
      previewUserLongitude: Number.NaN,
    });
    expect(pos).toEqual({
      latitude: DEFAULT_PIN_LATITUDE,
      longitude: DEFAULT_PIN_LONGITUDE,
    });
  });
});

describe('overrideFromMapPin', () => {
  const baseCandidate: PlaceCandidate = {
    displayName: 'Ngong Road, Nairobi West',
    latitude: -1.303,
    longitude: 36.79,
    localityId: '11111111-1111-1111-1111-111111111111',
    wardName: 'Nairobi West',
    cityName: 'Nairobi',
  };

  it('produces a ComposerLocationOverride with source=map_pin on a valid candidate', () => {
    const override = overrideFromMapPin(baseCandidate);
    expect(override).not.toBeNull();
    expect(override).toEqual({
      latitude: -1.303,
      longitude: 36.79,
      label: 'Ngong Road, Nairobi West',
      source: 'map_pin',
    });
  });

  it.each([
    ['empty displayName', { ...baseCandidate, displayName: '' }],
    ['whitespace displayName', { ...baseCandidate, displayName: '   ' }],
  ])('refuses to build an override when %s', (_desc, bad) => {
    expect(overrideFromMapPin(bad as PlaceCandidate)).toBeNull();
  });

  it.each([
    ['lat > 90', { ...baseCandidate, latitude: 95 }],
    ['lat < -90', { ...baseCandidate, latitude: -95 }],
    ['lng > 180', { ...baseCandidate, longitude: 200 }],
    ['lng < -180', { ...baseCandidate, longitude: -200 }],
  ])('refuses to build an override when %s', (_desc, bad) => {
    expect(overrideFromMapPin(bad as PlaceCandidate)).toBeNull();
  });
});

describe('MAP_PICKED_VIA constant', () => {
  it('is "map" — the UI-only pickedVia flag for the draggable pin path', () => {
    // Exercised by ComposerContext typing; this test anchors the literal
    // so a future rename cannot silently drift the subtitle copy.
    expect(MAP_PICKED_VIA).toBe('map');
  });
});
