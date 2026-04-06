// apps/citizen-mobile/__tests__/utils/contextWindow.test.ts
//
// Tests the 2-minute context window helpers. Pure functions — no time
// mocking needed because both `now` and `affectedAt` are passed in.

import {
  CONTEXT_WINDOW_MS,
  isContextWindowOpen,
  secondsRemaining,
} from '../../src/utils/contextWindow';

const T0 = 1_700_000_000_000; // arbitrary epoch ms

describe('CONTEXT_WINDOW_MS', () => {
  it('is exactly 2 minutes', () => {
    expect(CONTEXT_WINDOW_MS).toBe(120_000);
  });
});

describe('isContextWindowOpen', () => {
  it('returns false when affectedAt is null', () => {
    expect(isContextWindowOpen(null, T0)).toBe(false);
  });

  it('returns true immediately after affected (delta = 0)', () => {
    expect(isContextWindowOpen(T0, T0)).toBe(true);
  });

  it('returns true at 1 minute in', () => {
    expect(isContextWindowOpen(T0, T0 + 60_000)).toBe(true);
  });

  it('returns true at 1m59s in', () => {
    expect(isContextWindowOpen(T0, T0 + 119_000)).toBe(true);
  });

  it('returns false exactly at the boundary (2m, strict less-than)', () => {
    expect(isContextWindowOpen(T0, T0 + 120_000)).toBe(false);
  });

  it('returns false at 2m1s', () => {
    expect(isContextWindowOpen(T0, T0 + 121_000)).toBe(false);
  });

  it('returns false long after the window', () => {
    expect(isContextWindowOpen(T0, T0 + 60 * 60 * 1000)).toBe(false);
  });
});

describe('secondsRemaining', () => {
  it('returns 0 when affectedAt is null', () => {
    expect(secondsRemaining(null, T0)).toBe(0);
  });

  it('returns 120 immediately after affected', () => {
    expect(secondsRemaining(T0, T0)).toBe(120);
  });

  it('returns 60 at the 1-minute mark', () => {
    expect(secondsRemaining(T0, T0 + 60_000)).toBe(60);
  });

  it('returns 1 at the 1m59s mark', () => {
    expect(secondsRemaining(T0, T0 + 119_000)).toBe(1);
  });

  it('returns 0 exactly at the boundary', () => {
    expect(secondsRemaining(T0, T0 + 120_000)).toBe(0);
  });

  it('clamps to 0 past the window', () => {
    expect(secondsRemaining(T0, T0 + 200_000)).toBe(0);
  });

  it('rounds up sub-second remainders (501ms left → 1s)', () => {
    expect(secondsRemaining(T0, T0 + 119_499)).toBe(1);
  });
});
