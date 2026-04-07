// apps/citizen-mobile/src/utils/participationApi.ts
//
// Pure converter from the mobile's snake_case ParticipationType union to
// the wire format the backend's POST /v1/clusters/{id}/participation
// endpoint accepts.
//
// Background: ClustersController.cs uses Enum.TryParse<ParticipationType>(
//   dto.Type, ignoreCase: true, ...) against the C# enum:
//
//   public enum ParticipationType {
//     Affected, Observing, NoLongerAffected,
//     RestorationYes, RestorationNo, RestorationUnsure
//   }
//
// `ignoreCase: true` is case-insensitive but does NOT understand
// snake_case → PascalCase, so:
//   "affected"            → matches Affected           ✅
//   "observing"           → matches Observing          ✅
//   "no_longer_affected"  → does NOT match (rejected as 422)
//   "NoLongerAffected"    → matches                    ✅
//   "nolongeraffected"    → matches (case-insensitive) ✅
//
// The endpoint also caps participation type at index 2, so the restoration
// types must NOT be sent here — they go to /restoration-response instead.
// This converter only maps the three valid participation types.

import type { ParticipationType } from '../types/api';

/**
 * The three string values the participation endpoint will accept,
 * verified against ClustersController.RecordParticipation.
 */
export type ParticipationApiValue =
  | 'Affected'
  | 'Observing'
  | 'NoLongerAffected';

/**
 * Convert a mobile ParticipationType to its on-the-wire value.
 *
 * Throws for restoration types — those are not valid here. The
 * restoration vote endpoint accepts a different vocabulary entirely
 * (`restored` / `still_affected` / `not_sure`).
 */
export function participationTypeToBackend(
  type: ParticipationType,
): ParticipationApiValue {
  switch (type) {
    case 'affected':
      return 'Affected';
    case 'observing':
      return 'Observing';
    case 'no_longer_affected':
      return 'NoLongerAffected';
    case 'restoration_yes':
    case 'restoration_no':
    case 'restoration_unsure':
      throw new Error(
        `participationTypeToBackend: '${type}' is a restoration value — ` +
          'use the /restoration-response endpoint instead.',
      );
  }
}
