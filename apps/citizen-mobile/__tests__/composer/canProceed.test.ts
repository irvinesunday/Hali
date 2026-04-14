// apps/citizen-mobile/__tests__/composer/canProceed.test.ts
//
// Composer-level "Next button enablement" logic, extracted from
// confirm.tsx so it can be unit-tested without React Native.
//
// The 'fallback' tier regression tests (see "B-1") are the reason
// this helper exists: a preview with a non-empty NLP label and
// confidence < 0.5 previously let the user tap Next without engaging
// the picker, silently bypassing the fallback gate.

import {
  canProceedFromLocationGate,
  type LocationProceedInput,
} from '../../src/utils/composerGates';

const DEFAULT: Omit<LocationProceedInput, 'gate' | 'hasOverride'> = {
  label: '',
  originalLabel: '',
  confirmed: false,
};

describe('canProceedFromLocationGate', () => {
  describe('override wins regardless of gate', () => {
    it.each(['accept', 'confirm', 'fallback', 'required'] as const)(
      'returns true when hasOverride=true at gate=%s',
      (gate) => {
        expect(
          canProceedFromLocationGate({ ...DEFAULT, gate, hasOverride: true }),
        ).toBe(true);
      },
    );

    it('override wins even when the text-input label is empty', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'fallback',
          hasOverride: true,
          label: '',
          originalLabel: 'Ngong Rd',
          confirmed: false,
        }),
      ).toBe(true);
    });
  });

  describe('accept tier', () => {
    it('always allows proceed with no action', () => {
      expect(
        canProceedFromLocationGate({
          ...DEFAULT,
          gate: 'accept',
          hasOverride: false,
        }),
      ).toBe(true);
    });
  });

  describe('confirm tier', () => {
    it('blocks proceed until the user confirms or edits', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'confirm',
          hasOverride: false,
          label: 'Ngong Road',
          originalLabel: 'Ngong Road',
          confirmed: false,
        }),
      ).toBe(false);
    });

    it('allows proceed after user taps "Looks right"', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'confirm',
          hasOverride: false,
          label: 'Ngong Road',
          originalLabel: 'Ngong Road',
          confirmed: true,
        }),
      ).toBe(true);
    });

    it('allows proceed when the user edits the label to something non-empty and different', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'confirm',
          hasOverride: false,
          label: 'Lusaka Road',
          originalLabel: 'Ngong Road',
          confirmed: false,
        }),
      ).toBe(true);
    });

    it('does not treat a blank edit as a user edit', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'confirm',
          hasOverride: false,
          label: '   ',
          originalLabel: 'Ngong Road',
          confirmed: false,
        }),
      ).toBe(false);
    });
  });

  describe('fallback tier — override-only (B-1 regression)', () => {
    // Before the fix, this path returned true because the switch
    // branch read the stale NLP label out of the text-input state.
    // With fallback rendering the picker (and not the text input),
    // a non-empty NLP label cannot be treated as a user correction.
    it('blocks proceed when preview carries a non-empty NLP label but no override is picked', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'fallback',
          hasOverride: false,
          label: 'somewhere in Nairobi West',
          originalLabel: 'somewhere in Nairobi West',
          confirmed: false,
        }),
      ).toBe(false);
    });

    it('blocks proceed when the label has been edited but no override is picked', () => {
      // The picker is the only authoritative correction in this tier;
      // an edit to the hidden text input state must not unlock Next.
      expect(
        canProceedFromLocationGate({
          gate: 'fallback',
          hasOverride: false,
          label: 'user typed something',
          originalLabel: 'somewhere in Nairobi West',
          confirmed: false,
        }),
      ).toBe(false);
    });

    it('blocks proceed when label is blank and no override is picked', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'fallback',
          hasOverride: false,
          label: '',
          originalLabel: null,
          confirmed: false,
        }),
      ).toBe(false);
    });

    it('allows proceed when an override is present (happy-path complement to the bug test)', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'fallback',
          hasOverride: true,
          label: 'somewhere in Nairobi West',
          originalLabel: 'somewhere in Nairobi West',
          confirmed: false,
        }),
      ).toBe(true);
    });
  });

  describe("required tier (not emitted by location gate today)", () => {
    // Defensive coverage: classifyLocationGate never returns 'required'
    // for location, but the union still contains it because the
    // condition classifier uses it. If a future change starts emitting
    // 'required' for location, the helper falls back to requiring a
    // non-empty label rather than unlocking Next.
    it('requires a non-empty label when gate is required', () => {
      expect(
        canProceedFromLocationGate({
          gate: 'required',
          hasOverride: false,
          label: 'Lusaka Road',
          originalLabel: null,
          confirmed: false,
        }),
      ).toBe(true);
      expect(
        canProceedFromLocationGate({
          gate: 'required',
          hasOverride: false,
          label: '',
          originalLabel: null,
          confirmed: false,
        }),
      ).toBe(false);
    });
  });
});
