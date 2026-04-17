// apps/citizen-mobile/__tests__/composer/locationGate.test.ts
//
// Tests the location confidence gate classifier used by the composer's
// Step 2 (confirm.tsx). Pure function — no React Native imports, no
// mocks, no module state.
//
// Thresholds (from src/config/constants.ts):
//   LOCATION_CONFIDENCE_WARN_THRESHOLD  = 0.5
//   LOCATION_CONFIDENCE_AMBER_THRESHOLD = 0.8
//
// C11 tiers (see composerGates.ts):
//   confidence < 0.5              → 'fallback'  (place-search / current-location picker)
//   0.5 ≤ confidence < 0.8        → 'confirm'   (amber, confirm or edit)
//   confidence ≥ 0.8              → 'accept'    (pre-filled, no action)
//
// Additional rule: the server flag wins, and a blank label at any
// confidence drops the tier to 'fallback'.

import { classifyLocationGate } from '../../src/utils/composerGates';

describe('classifyLocationGate — legacy numeric form', () => {
  describe('fallback tier (< 0.5)', () => {
    it('classifies 0.0 as fallback', () => {
      expect(classifyLocationGate(0)).toBe('fallback');
    });

    it('classifies 0.25 as fallback', () => {
      expect(classifyLocationGate(0.25)).toBe('fallback');
    });

    it('classifies 0.49 as fallback', () => {
      expect(classifyLocationGate(0.49)).toBe('fallback');
    });

    it('classifies 0.4999 as fallback (just below boundary)', () => {
      expect(classifyLocationGate(0.4999)).toBe('fallback');
    });
  });

  describe('confirm tier (0.5 ≤ x < 0.8)', () => {
    it('classifies exactly 0.5 as confirm (lower boundary inclusive)', () => {
      expect(classifyLocationGate(0.5)).toBe('confirm');
    });

    it('classifies 0.65 as confirm', () => {
      expect(classifyLocationGate(0.65)).toBe('confirm');
    });

    it('classifies 0.79 as confirm', () => {
      expect(classifyLocationGate(0.79)).toBe('confirm');
    });

    it('classifies 0.7999 as confirm (just below upper boundary)', () => {
      expect(classifyLocationGate(0.7999)).toBe('confirm');
    });
  });

  describe('accept tier (≥ 0.8)', () => {
    it('classifies exactly 0.8 as accept (upper boundary inclusive)', () => {
      expect(classifyLocationGate(0.8)).toBe('accept');
    });

    it('classifies 0.9 as accept', () => {
      expect(classifyLocationGate(0.9)).toBe('accept');
    });

    it('classifies 1.0 as accept', () => {
      expect(classifyLocationGate(1.0)).toBe('accept');
    });
  });

  describe('out-of-range inputs', () => {
    // The backend constrains confidence to [0, 1], but the classifier
    // should never throw regardless — it just applies the same threshold
    // logic. This documents behaviour for safety.
    it('treats values > 1 as accept', () => {
      expect(classifyLocationGate(1.5)).toBe('accept');
    });

    it('treats negative values as fallback', () => {
      expect(classifyLocationGate(-0.1)).toBe('fallback');
    });
  });
});

describe('classifyLocationGate — C11 object form', () => {
  describe('server flag wins', () => {
    it('returns fallback when requiresFallback=true even at high confidence', () => {
      expect(
        classifyLocationGate({
          confidence: 0.95,
          requiresFallback: true,
          label: 'Ngong Road',
        }),
      ).toBe('fallback');
    });

    it('still classifies normally when requiresFallback=false', () => {
      expect(
        classifyLocationGate({
          confidence: 0.9,
          requiresFallback: false,
          label: 'Ngong Road',
        }),
      ).toBe('accept');
    });

    it('ignores an undefined server flag and uses client-side rule', () => {
      expect(
        classifyLocationGate({ confidence: 0.3, label: 'Ngong Road' }),
      ).toBe('fallback');
    });
  });

  describe('blank label drops tier to fallback', () => {
    it('treats a null label at high confidence as fallback', () => {
      expect(
        classifyLocationGate({ confidence: 0.95, label: null }),
      ).toBe('fallback');
    });

    it('treats an empty-string label at high confidence as fallback', () => {
      expect(classifyLocationGate({ confidence: 0.95, label: '' })).toBe(
        'fallback',
      );
    });

    it('treats a whitespace-only label at high confidence as fallback', () => {
      expect(
        classifyLocationGate({ confidence: 0.95, label: '   ' }),
      ).toBe('fallback');
    });

    it('does not apply the label check when label arg is omitted', () => {
      // Legacy bare-number contract must still return 'accept' on high
      // confidence without a label argument.
      expect(classifyLocationGate({ confidence: 0.95 })).toBe('accept');
    });

    it('non-blank label at mid confidence remains confirm', () => {
      expect(
        classifyLocationGate({ confidence: 0.6, label: 'Ngong Road' }),
      ).toBe('confirm');
    });
  });
});
