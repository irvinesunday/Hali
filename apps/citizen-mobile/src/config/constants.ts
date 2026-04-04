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
export const SIGNAL_TEXT_MAX_LENGTH = 500;
export const CONTEXT_TEXT_MAX_LENGTH = 150;
export const LOCATION_CONFIDENCE_WARN_THRESHOLD = 0.5;
export const LOCATION_CONFIDENCE_AMBER_THRESHOLD = 0.8;
