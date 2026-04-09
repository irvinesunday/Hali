// Secure store key names — must match across the app
export const SECURE_STORE_KEYS = {
  ACCESS_TOKEN: 'HALI_ACCESS_TOKEN',
  REFRESH_TOKEN: 'HALI_REFRESH_TOKEN',
  ACCOUNT_ID: 'HALI_ACCOUNT_ID',
} as const;

// Participation type strings (non-negotiable — from mobile_screen_inventory.md)
export const PARTICIPATION_LABELS = {
  affected: "I'm Affected",
  observing: "I'm Observing",
  no_longer_affected: 'No Longer Affected',
  restoration_yes: 'Service Restored for Me',
  restoration_no: 'Still Affected',
  restoration_unsure: 'Not Sure Yet',
} as const;

export const MAX_FOLLOWED_WARDS = 5;

// Feature flag — gates the GPS opt-in button on the wards settings screen.
// The backend endpoint GET /v1/localities/resolve-by-coordinates is currently
// a stub that always returns 404. Flip this to true once the backend is
// implemented (see LocalitiesController.ResolveByCoordinates).
export const FEATURE_GPS_LOCALITY_OPT_IN = false;
export const SIGNAL_TEXT_MAX_LENGTH = 500;
export const CONTEXT_TEXT_MAX_LENGTH = 150;
// Location confidence gates for the signal composer:
//   confidence < WARN  → empty field, user MUST enter location before proceeding
//   WARN ≤ confidence < AMBER → amber warning, user can correct or accept
//   confidence ≥ AMBER → pre-filled, no mandatory confirmation
export const LOCATION_CONFIDENCE_WARN_THRESHOLD = 0.5;
export const LOCATION_CONFIDENCE_AMBER_THRESHOLD = 0.8;

// Condition confidence gates — same structure, different thresholds per spec.
// NOT yet enforced in the composer UI: the mobile app has no taxonomy data
// (slug list per category) so a correction dropdown cannot be rendered.
// When /packages/taxonomy is integrated, confirm.tsx should read these
// constants and implement the same three-tier gate as location.
export const CONDITION_CONFIDENCE_WARN_THRESHOLD = 0.5;
export const CONDITION_CONFIDENCE_AMBER_THRESHOLD = 0.75;
