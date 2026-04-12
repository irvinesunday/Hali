// apps/citizen-mobile/src/utils/phone.ts
//
// Pure phone-number utilities for Kenya (+254).
// No React Native imports — safe to use in tests, workers, or shared code.

/**
 * Normalise user input to an E.164 Kenyan phone number.
 * Accepts: 07XXXXXXXX, 7XXXXXXXX, +2547XXXXXXXX, 2547XXXXXXXX
 * Returns null if the input does not look like a valid Kenyan number.
 */
export function normaliseKenyaPhone(raw: string): string | null {
  const digits = raw.replace(/\D/g, '');

  // Full international: 2547XXXXXXXX (12 digits)
  if (digits.startsWith('254') && digits.length === 12) {
    return `+${digits}`;
  }
  // Local with leading zero: 07XXXXXXXX (10 digits)
  if (digits.startsWith('0') && digits.length === 10) {
    return `+254${digits.slice(1)}`;
  }
  // Local without leading zero: 7XXXXXXXX (9 digits)
  if (digits.length === 9) {
    return `+254${digits}`;
  }
  return null;
}

export function isValidKenyaPhoneInput(raw: string): boolean {
  return normaliseKenyaPhone(raw) !== null;
}
