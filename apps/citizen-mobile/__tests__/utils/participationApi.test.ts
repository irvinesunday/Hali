// apps/citizen-mobile/__tests__/utils/participationApi.test.ts
//
// Tests the snake_case → PascalCase converter for the participation
// endpoint. Locks the contract surfaced by Enum.TryParse in
// ClustersController.RecordParticipation.

import { participationTypeToBackend } from '../../src/utils/participationApi';

describe('participationTypeToBackend', () => {
  it('maps affected → Affected', () => {
    expect(participationTypeToBackend('affected')).toBe('Affected');
  });

  it('maps observing → Observing', () => {
    expect(participationTypeToBackend('observing')).toBe('Observing');
  });

  it('maps no_longer_affected → NoLongerAffected (no underscore)', () => {
    // Critical: Enum.TryParse does NOT understand snake_case, so the
    // wire value must collapse the underscores. This is the bug fix.
    expect(participationTypeToBackend('no_longer_affected')).toBe(
      'NoLongerAffected',
    );
  });

  it('throws for restoration_yes (wrong endpoint)', () => {
    expect(() => participationTypeToBackend('restoration_yes')).toThrow(
      /restoration value/,
    );
  });

  it('throws for restoration_no (wrong endpoint)', () => {
    expect(() => participationTypeToBackend('restoration_no')).toThrow(
      /restoration value/,
    );
  });

  it('throws for restoration_unsure (wrong endpoint)', () => {
    expect(() => participationTypeToBackend('restoration_unsure')).toThrow(
      /restoration value/,
    );
  });

  // Defensive: the error message should mention the offending value
  // so a future caller can debug without reading the source.
  it('mentions the offending value in the thrown error message', () => {
    expect(() => participationTypeToBackend('restoration_yes')).toThrow(
      /restoration_yes/,
    );
  });
});
