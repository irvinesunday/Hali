// apps/citizen-mobile/src/utils/offlineQueueLogic.ts
//
// Pure functions powering the offline write queue. Extracted from
// src/lib/offlineQueue.ts so the queueing/flushing rules can be unit
// tested without AsyncStorage, NetInfo, or AppState.
//
// Glossary:
//   QueueItem  — a write request waiting to be flushed
//   FlushResult — the outcome of attempting to flush a single item:
//                  'sent'    — server accepted (or 4xx that's not network)
//                  'retry'   — network error / 5xx — keep in queue
//                  'dropped' — server rejected (4xx) — remove from queue
//
// The queue lives in AsyncStorage under a single JSON-array key. The
// runtime layer (offlineQueue.ts) is responsible for I/O and retries;
// this file is responsible for the *rules* governing what stays and
// what goes.

import type { ApiError, Result } from '../types/api';

/**
 * The four write paths the queue handles. Listing them as a closed
 * union (vs. accepting any string) prevents typos and makes the
 * queue's surface area visible at a glance.
 */
export type OfflineQueueKind =
  | 'signal_submit'
  | 'participation'
  | 'restoration_response'
  | 'context';

export interface QueueItem {
  /** Stable client-generated id (UUID-like, used for queue de-dup). */
  id: string;
  /** Which write endpoint this item targets. */
  kind: OfflineQueueKind;
  /** Full request path including any path params. */
  endpoint: string;
  /** Body payload as it would be sent over the wire. */
  body: Record<string, unknown>;
  /** Idempotency key generated client-side BEFORE first send attempt. */
  idempotencyKey: string;
  /** ISO timestamp the user took the action. */
  createdAt: string;
  /** How many flush attempts have been made (informational). */
  attempts: number;
}

export type FlushOutcome = 'sent' | 'retry' | 'dropped';

/**
 * Decide what to do with an item after a flush attempt.
 *
 * Rules (intentionally simple — the queue is not a circuit breaker):
 *   - Result.ok                       → 'sent' (remove from queue)
 *   - status === 0 (network_error)    → 'retry' (keep in queue)
 *   - status >= 500                   → 'retry' (server error, transient)
 *   - status 409 (duplicate idempotency) → 'sent' (already accepted earlier)
 *   - other 4xx (400/401/403/422 etc.) → 'dropped' (permanent client error;
 *                                         leaving it in the queue would just
 *                                         keep failing every flush)
 */
export function classifyFlushResult<T>(
  result: Result<T, ApiError>,
): FlushOutcome {
  if (result.ok) return 'sent';
  const status = result.error.status;
  if (status === 0) return 'retry'; // network error per buildApiError
  if (status >= 500) return 'retry';
  if (status === 409) return 'sent'; // idempotency replay — server already saw it
  return 'dropped';
}

/**
 * Apply a flush outcome to the queue, returning the new queue.
 * Pure function — does not mutate the input.
 */
export function applyFlushOutcome(
  queue: readonly QueueItem[],
  itemId: string,
  outcome: FlushOutcome,
): QueueItem[] {
  switch (outcome) {
    case 'sent':
    case 'dropped':
      return queue.filter((item) => item.id !== itemId);
    case 'retry':
      return queue.map((item) =>
        item.id === itemId ? { ...item, attempts: item.attempts + 1 } : item,
      );
  }
}

/**
 * Append a new item to the queue, de-duped on idempotency key.
 * If an item with the same idempotency key already exists, the queue
 * is returned unchanged (the existing item still represents the same
 * logical write).
 */
export function enqueueItem(
  queue: readonly QueueItem[],
  item: QueueItem,
): QueueItem[] {
  if (queue.some((q) => q.idempotencyKey === item.idempotencyKey)) {
    return [...queue];
  }
  return [...queue, item];
}

/**
 * Generate a queue-item id. Not cryptographic — just unique enough for
 * the queue's de-dup needs. The idempotency key is the actual contract
 * between client and server; this is purely an internal handle.
 */
export function newQueueItemId(now: number, rand: number): string {
  return `q_${now.toString(36)}_${Math.floor(rand * 1e9).toString(36)}`;
}
