// apps/citizen-mobile/__tests__/utils/localityFilter.test.ts
//
// Unit tests for the C10 searchable ward selection filter utility.
// filterLocalities powers the home locality picker's instant, client-side
// search over the cached ward list.

import { filterLocalities } from '../../src/utils/localityFilter';
import type { LocalitySummary } from '../../src/types/api';

const wards: LocalitySummary[] = [
  { localityId: 'a', wardName: 'South B',      cityName: 'Nairobi' },
  { localityId: 'b', wardName: 'Umoja I',      cityName: 'Nairobi' },
  { localityId: 'c', wardName: 'Umoja II',     cityName: 'Nairobi' },
  { localityId: 'd', wardName: 'Karen',        cityName: 'Nairobi' },
  { localityId: 'e', wardName: 'Kasarani',     cityName: 'Nairobi' },
  { localityId: 'f', wardName: 'Westlands',    cityName: null },
];

describe('filterLocalities', () => {
  it('returns the full list when the query is empty', () => {
    expect(filterLocalities(wards, '')).toEqual(wards);
  });

  it('returns the full list when the query is whitespace-only', () => {
    expect(filterLocalities(wards, '   ')).toEqual(wards);
    expect(filterLocalities(wards, '\t\n')).toEqual(wards);
  });

  it('does not mutate the input list', () => {
    const snapshot = [...wards];
    filterLocalities(wards, 'ka');
    expect(wards).toEqual(snapshot);
  });

  it('matches case-insensitively', () => {
    const lower = filterLocalities(wards, 'karen');
    const upper = filterLocalities(wards, 'KAREN');
    const mixed = filterLocalities(wards, 'KaReN');
    expect(lower.map((w) => w.localityId)).toEqual(['d']);
    expect(upper).toEqual(lower);
    expect(mixed).toEqual(lower);
  });

  it('trims leading and trailing whitespace in the query', () => {
    const trimmed = filterLocalities(wards, '  karen  ');
    expect(trimmed.map((w) => w.localityId)).toEqual(['d']);
  });

  it('matches partial substrings within ward name', () => {
    const umoja = filterLocalities(wards, 'umoj');
    expect(umoja.map((w) => w.localityId).sort()).toEqual(['b', 'c']);
  });

  it('matches a single-character query', () => {
    // Forgiving UX: the filter does not enforce a minimum query length;
    // the caller decides when to start filtering.
    const result = filterLocalities(wards, 'k');
    expect(result.map((w) => w.localityId).sort()).toEqual(['d', 'e']);
  });

  it('falls back to cityName when ward name does not match', () => {
    // "nairobi" is in cityName, not wardName — still a hit.
    const result = filterLocalities(wards, 'nairobi');
    expect(result.length).toBe(5); // every ward with cityName === 'Nairobi'
    expect(result.every((w) => w.cityName === 'Nairobi')).toBe(true);
    // Westlands (cityName: null) must be excluded.
    expect(result.some((w) => w.localityId === 'f')).toBe(false);
  });

  it('does not throw when cityName is null', () => {
    expect(() => filterLocalities(wards, 'west')).not.toThrow();
    expect(filterLocalities(wards, 'west').map((w) => w.localityId)).toEqual(['f']);
  });

  it('returns an empty array when nothing matches', () => {
    expect(filterLocalities(wards, 'zzzzz')).toEqual([]);
  });

  it('preserves the input ordering', () => {
    // Input is ordered alphabetically; the filter must not reshuffle.
    const result = filterLocalities(wards, 'a');
    const ids = result.map((w) => w.localityId);
    // Should match the relative order in `wards`.
    const inputOrder = wards.map((w) => w.localityId).filter((id) => ids.includes(id));
    expect(ids).toEqual(inputOrder);
  });
});
