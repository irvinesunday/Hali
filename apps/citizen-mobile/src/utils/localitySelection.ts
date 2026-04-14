// apps/citizen-mobile/src/utils/localitySelection.ts
//
// Pure routing for a ward tap in the locality picker.
//
// The home picker can surface three kinds of rows:
//   - a ward the user already follows ("Your areas")
//   - an unfollowed ward in the canonical list ("Browse all wards")
//   - any ward that matches an active search query ("Matches")
//
// In all three cases the user eventually expects the home feed to
// reflect the tapped ward. A followed tap routes through
// setActiveLocalityId so the user's stored displayLabel survives;
// an unfollowed tap sets the active locality directly. This helper
// is the pure core of that branch so it can be unit-tested without
// RN/Jest host wiring.
//
// Kept deliberately free of React / side effects. UI code calls this,
// then invokes the appropriate LocalityContext setter itself.

import type { FollowedLocality, LocalitySummary } from '../types/api';

/**
 * Describes how a ward tap should be applied to LocalityContext.
 *
 * - `existingFollow` — the ward is already in the user's follow set;
 *   the caller should call `setActiveLocalityId(localityId)` so the
 *   stored displayLabel and cityName from the follow record are
 *   preserved.
 * - `directLocality` — the ward is not followed; the caller should
 *   call `setActiveLocality(locality)` directly. The helper composes
 *   a sensible displayLabel (`"wardName, cityName"` when cityName is
 *   present) so the home header stays informative for guest browse
 *   and ad-hoc lookups.
 */
export type LocalitySelection =
  | { kind: 'existingFollow'; localityId: string }
  | { kind: 'directLocality'; locality: FollowedLocality };

export function resolveLocalitySelection(
  ward: LocalitySummary,
  followedLocalities: readonly FollowedLocality[],
): LocalitySelection {
  const isFollowed = followedLocalities.some(
    (l) => l.localityId === ward.localityId,
  );

  if (isFollowed) {
    return { kind: 'existingFollow', localityId: ward.localityId };
  }

  return {
    kind: 'directLocality',
    locality: {
      localityId: ward.localityId,
      wardName: ward.wardName,
      displayLabel: ward.cityName
        ? `${ward.wardName}, ${ward.cityName}`
        : null,
      cityName: ward.cityName,
    },
  };
}
