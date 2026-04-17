// apps/citizen-mobile/__tests__/utils/idempotency.test.ts
//
// Tests the pure idempotency-input building (the SHA-256 wrapper itself
// goes through expo-crypto and is exercised by integration tests).

import {
  buildIdempotencyInput,
  stableStringify,
} from '../../src/utils/idempotency';

describe('stableStringify', () => {
  it('serialises null and primitives correctly', () => {
    expect(stableStringify(null)).toBe('null');
    expect(stableStringify(42)).toBe('42');
    expect(stableStringify(true)).toBe('true');
    expect(stableStringify('hello')).toBe('"hello"');
  });

  it('escapes strings with quotes', () => {
    expect(stableStringify('a"b')).toBe('"a\\"b"');
  });

  it('preserves array order (arrays are intentionally ordered)', () => {
    expect(stableStringify([3, 1, 2])).toBe('[3,1,2]');
  });

  it('sorts object keys alphabetically', () => {
    expect(stableStringify({ b: 1, a: 2 })).toBe('{"a":2,"b":1}');
  });

  it('produces identical output regardless of property insertion order', () => {
    const a = { name: 'x', count: 1, active: true };
    const b = { active: true, count: 1, name: 'x' };
    expect(stableStringify(a)).toBe(stableStringify(b));
  });

  it('recurses into nested objects with their own sorted keys', () => {
    expect(
      stableStringify({ outer: { z: 1, a: 2 }, alpha: 'x' }),
    ).toBe('{"alpha":"x","outer":{"a":2,"z":1}}');
  });

  it('omits undefined values from objects (matches JSON.stringify)', () => {
    expect(stableStringify({ a: 1, b: undefined, c: 2 })).toBe('{"a":1,"c":2}');
  });

  it('omits function values from objects', () => {
    const obj = { a: 1, b: () => 42 };
    expect(stableStringify(obj)).toBe('{"a":1}');
  });
});

describe('buildIdempotencyInput', () => {
  it('joins endpoint and stable body with the :: delimiter', () => {
    expect(
      buildIdempotencyInput('/v1/signals/submit', { freeText: 'water out' }),
    ).toBe('/v1/signals/submit::{"freeText":"water out"}');
  });

  it('produces identical input for the same logical request', () => {
    // Same body, different property insertion order
    const a = buildIdempotencyInput('/v1/clusters/abc/participation', {
      type: 'Affected',
      deviceHash: 'h',
      idempotencyKey: 'k',
    });
    const b = buildIdempotencyInput('/v1/clusters/abc/participation', {
      idempotencyKey: 'k',
      deviceHash: 'h',
      type: 'Affected',
    });
    expect(a).toBe(b);
  });

  it('produces different input for different endpoints', () => {
    const a = buildIdempotencyInput('/v1/signals/submit', { freeText: 'x' });
    const b = buildIdempotencyInput('/v1/signals/preview', { freeText: 'x' });
    expect(a).not.toBe(b);
  });

  it('produces different input for different bodies', () => {
    const a = buildIdempotencyInput('/v1/signals/submit', { freeText: 'a' });
    const b = buildIdempotencyInput('/v1/signals/submit', { freeText: 'b' });
    expect(a).not.toBe(b);
  });
});
