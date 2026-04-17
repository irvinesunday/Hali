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
    expect(computeInitialPinPosition(override)).toEqual({
      latitude: -1.303,
      longitude: 36.79,
    });
  });

  it('falls back to the Nairobi default when no override is set', () => {
    expect(computeInitialPinPosition(null)).toEqual({
      latitude: DEFAULT_PIN_LATITUDE,
      longitude: DEFAULT_PIN_LONGITUDE,
    });
  });

  it('rejects an out-of-bounds override and falls back to the default', () => {
    const badOverride: ComposerLocationOverride = {
      latitude: 95, // > 90
      longitude: 36.8,
      label: 'garbage',
      source: 'place_search',
    };
    expect(computeInitialPinPosition(badOverride)).toEqual({
      latitude: DEFAULT_PIN_LATITUDE,
      longitude: DEFAULT_PIN_LONGITUDE,
    });
  });

  it('rejects a non-finite override and falls back to the default', () => {
    const badOverride: ComposerLocationOverride = {
      latitude: Number.NaN,
      longitude: Number.NaN,
      label: 'garbage',
      source: 'place_search',
    };
    expect(computeInitialPinPosition(badOverride)).toEqual({
      latitude: DEFAULT_PIN_LATITUDE,
      longitude: DEFAULT_PIN_LONGITUDE,
    });
  });

  it('accepts a bounds-inclusive map_pin override (refining a previous pin)', () => {
    // Guards the refinement loop: the user dropped a pin, went back to
    // confirm, returned to the pin screen — the previous pin should
    // still be the starting point, not the Nairobi default.
    const override: ComposerLocationOverride = {
      latitude: -1.2855,
      longitude: 36.8305,
      label: 'Juja Road, Kariokor',
      source: 'map_pin',
    };
    expect(computeInitialPinPosition(override)).toEqual({
      latitude: -1.2855,
      longitude: 36.8305,
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
