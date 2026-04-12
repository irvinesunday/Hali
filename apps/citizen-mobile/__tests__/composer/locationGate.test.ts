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
// Expected classification:
//   confidence < 0.5              → 'required'  (empty field, MUST fill)
//   0.5 ≤ confidence < 0.8        → 'confirm'   (amber, MUST confirm or edit)
//   confidence ≥ 0.8              → 'accept'    (pre-filled, no action)

// Pure utility — no React Native imports, fully testable under Jest.
import { classifyLocationGate } from '../../src/utils/composerGates';

describe('classifyLocationGate', () => {
  describe('required tier (< 0.5)', () => {
    it('classifies 0.0 as required', () => {
      expect(classifyLocationGate(0)).toBe('required');
    });

    it('classifies 0.25 as required', () => {
      expect(classifyLocationGate(0.25)).toBe('required');
    });

    it('classifies 0.49 as required', () => {
      expect(classifyLocationGate(0.49)).toBe('required');
    });

    it('classifies 0.4999 as required (just below boundary)', () => {
      expect(classifyLocationGate(0.4999)).toBe('required');
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

    it('treats negative values as required', () => {
      expect(classifyLocationGate(-0.1)).toBe('required');
    });
  });
});
