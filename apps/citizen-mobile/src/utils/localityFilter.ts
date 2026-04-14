// apps/citizen-mobile/src/utils/localityFilter.ts
//
// Pure, synchronous filter used by the ward/locality picker.
// Kept separate from UI code so it can be unit-tested without RN/Jest
// host wiring.

import type { LocalitySummary } from '../types/api';

/**
 * Filter a list of {@link LocalitySummary} by user-entered search text.
 *
 * Matching rules (intentionally forgiving for a mobile search-as-you-type UX):
 *   - case-insensitive
 *   - whitespace-trimmed (leading/trailing)
 *   - substring match against `wardName` first, then `cityName`
 *   - empty / whitespace-only query returns the full list unchanged
 *
 * The input list is assumed to already be sorted (the backend returns
 * wards ordered alphabetically by ward name); this function preserves
 * that ordering.
 */
export function filterLocalities(
  localities: readonly LocalitySummary[],
  query: string,
): LocalitySummary[] {
  const normalized = query.trim().toLowerCase();
  if (normalized.length === 0) {
    return localities.slice();
  }

  return localities.filter((l) => {
    const ward = l.wardName.toLowerCase();
    if (ward.includes(normalized)) return true;
    const city = l.cityName?.toLowerCase() ?? '';
    return city.length > 0 && city.includes(normalized);
  });
}
