// apps/citizen-mobile/__tests__/utils/phone.test.ts
//
// Smoke test — confirms Jest + jest-expo setup is working.
// Tests the pure normaliseKenyaPhone utility (no React Native imports).

import {
  normaliseKenyaPhone,
  isValidKenyaPhoneInput,
} from '../../src/utils/phone';

describe('normaliseKenyaPhone', () => {
  it('accepts local format with leading zero (07XXXXXXXX)', () => {
    expect(normaliseKenyaPhone('0712345678')).toBe('+254712345678');
  });

  it('accepts local format without leading zero (7XXXXXXXX)', () => {
    expect(normaliseKenyaPhone('712345678')).toBe('+254712345678');
  });

  it('accepts full international format (+2547XXXXXXXX)', () => {
    expect(normaliseKenyaPhone('+254712345678')).toBe('+254712345678');
  });

  it('accepts international format without plus (2547XXXXXXXX)', () => {
    expect(normaliseKenyaPhone('254712345678')).toBe('+254712345678');
  });

  it('strips spaces, dashes, and parens before normalising', () => {
    expect(normaliseKenyaPhone('0712 345 678')).toBe('+254712345678');
    expect(normaliseKenyaPhone('+254-712-345-678')).toBe('+254712345678');
    expect(normaliseKenyaPhone('(0712) 345 678')).toBe('+254712345678');
  });

  it('rejects inputs that are too short', () => {
    expect(normaliseKenyaPhone('07123')).toBeNull();
    expect(normaliseKenyaPhone('712')).toBeNull();
  });

  it('rejects inputs that are too long', () => {
    expect(normaliseKenyaPhone('07123456789')).toBeNull();
    expect(normaliseKenyaPhone('2547123456789')).toBeNull();
  });

  it('rejects empty and whitespace-only inputs', () => {
    expect(normaliseKenyaPhone('')).toBeNull();
    expect(normaliseKenyaPhone('   ')).toBeNull();
  });
});

describe('isValidKenyaPhoneInput', () => {
  it('returns true for valid inputs', () => {
    expect(isValidKenyaPhoneInput('0712345678')).toBe(true);
    expect(isValidKenyaPhoneInput('+254712345678')).toBe(true);
  });

  it('returns false for invalid inputs', () => {
    expect(isValidKenyaPhoneInput('abc')).toBe(false);
    expect(isValidKenyaPhoneInput('')).toBe(false);
  });
});
