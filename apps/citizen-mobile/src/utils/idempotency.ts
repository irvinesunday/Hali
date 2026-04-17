// apps/citizen-mobile/src/utils/idempotency.ts
//
// Idempotency-key generation for write requests.
//
// Keys are SHA-256 hashes of a deterministic input string. The same input
// always produces the same key, so a queued retry with the same input will
// reuse the same key — required by the backend's idempotency check on
// /v1/signals/submit and /v1/clusters/{id}/participation.
//
// Why SHA-256: matches the auth/composer/cluster code paths that already
// use Crypto.digestStringAsync(SHA256). Consistent across the app.

import * as Crypto from 'expo-crypto';

/**
 * Build the canonical input string for an idempotency key.
 * Pure function — exported for testability.
 *
 * The shape is `${endpoint}::${stableBodyJson}` where stableBodyJson is
 * the body serialised with keys sorted alphabetically. This guarantees
 * the same logical request produces the same key, regardless of how
 * JavaScript orders object properties at construction time.
 */
export function buildIdempotencyInput(
  endpoint: string,
  body: Record<string, unknown>,
): string {
  return `${endpoint}::${stableStringify(body)}`;
}

/**
 * JSON.stringify that sorts object keys recursively. Arrays preserve
 * order. Primitives (string, number, boolean, null) are emitted as-is.
 * Undefined values and functions are omitted (matching JSON.stringify).
 */
export function stableStringify(value: unknown): string {
  if (value === null) return 'null';
  if (typeof value === 'number' || typeof value === 'boolean') {
    return JSON.stringify(value);
  }
  if (typeof value === 'string') return JSON.stringify(value);
  if (Array.isArray(value)) {
    return `[${value.map((v) => stableStringify(v)).join(',')}]`;
  }
  if (typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    const keys = Object.keys(obj)
      .filter((k) => obj[k] !== undefined && typeof obj[k] !== 'function')
      .sort();
    const entries = keys.map(
      (k) => `${JSON.stringify(k)}:${stableStringify(obj[k])}`,
    );
    return `{${entries.join(',')}}`;
  }
  // undefined, function — JSON.stringify drops these too
  return 'null';
}

/**
 * Async hash via expo-crypto. Resolves to a 64-char hex string.
 * Throws only if expo-crypto itself throws (extremely unlikely).
 */
export async function generateIdempotencyKey(
  endpoint: string,
  body: Record<string, unknown>,
): Promise<string> {
  const input = buildIdempotencyInput(endpoint, body);
  return Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, input);
}
