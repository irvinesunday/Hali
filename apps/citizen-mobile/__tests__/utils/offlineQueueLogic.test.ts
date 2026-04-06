// apps/citizen-mobile/__tests__/utils/offlineQueueLogic.test.ts
//
// Tests the pure flush-classification + queue-mutation logic. No
// AsyncStorage, NetInfo, or AppState involved — these functions take
// queue snapshots and return new ones.

import {
  applyFlushOutcome,
  classifyFlushResult,
  enqueueItem,
  newQueueItemId,
  type QueueItem,
} from '../../src/utils/offlineQueueLogic';

function makeItem(overrides: Partial<QueueItem> = {}): QueueItem {
  return {
    id: 'q_test_1',
    kind: 'signal_submit',
    endpoint: '/v1/signals/submit',
    body: { freeText: 'water out', idempotencyKey: 'fixed-key' },
    idempotencyKey: 'fixed-key',
    createdAt: '2026-04-06T12:00:00.000Z',
    attempts: 0,
    ...overrides,
  };
}

// ─── classifyFlushResult ───────────────────────────────────────────────

describe('classifyFlushResult', () => {
  it('classifies Result.ok as sent', () => {
    expect(classifyFlushResult({ ok: true, value: undefined })).toBe('sent');
  });

  it('classifies network_error (status 0) as retry', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 0, code: 'network_error', message: 'no network' },
      }),
    ).toBe('retry');
  });

  it('classifies 500 as retry (server error, transient)', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 500, code: 'server_error', message: 'oops' },
      }),
    ).toBe('retry');
  });

  it('classifies 503 as retry', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 503, code: 'unavailable', message: 'down' },
      }),
    ).toBe('retry');
  });

  it('classifies 409 as sent (idempotency replay — server already saw it)', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 409, code: 'duplicate', message: 'already submitted' },
      }),
    ).toBe('sent');
  });

  it('classifies 400 as dropped (permanent client error)', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 400, code: 'bad_request', message: 'missing field' },
      }),
    ).toBe('dropped');
  });

  it('classifies 401 as dropped', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: { status: 401, code: 'session_expired', message: 'expired' },
      }),
    ).toBe('dropped');
  });

  it('classifies 422 as dropped', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: {
          status: 422,
          code: 'context_edit_window_expired',
          message: 'too late',
        },
      }),
    ).toBe('dropped');
  });

  it('classifies 429 as dropped (rate limit — leaving in queue would just keep failing)', () => {
    expect(
      classifyFlushResult({
        ok: false,
        error: {
          status: 429,
          code: 'rate_limited',
          message: 'too many requests',
        },
      }),
    ).toBe('dropped');
  });
});

// ─── applyFlushOutcome ─────────────────────────────────────────────────

describe('applyFlushOutcome', () => {
  const a = makeItem({ id: 'a' });
  const b = makeItem({ id: 'b' });
  const c = makeItem({ id: 'c', attempts: 3 });
  const queue: QueueItem[] = [a, b, c];

  it("removes the matching item on 'sent'", () => {
    const next = applyFlushOutcome(queue, 'b', 'sent');
    expect(next).toHaveLength(2);
    expect(next.map((x) => x.id)).toEqual(['a', 'c']);
  });

  it("removes the matching item on 'dropped'", () => {
    const next = applyFlushOutcome(queue, 'a', 'dropped');
    expect(next.map((x) => x.id)).toEqual(['b', 'c']);
  });

  it("keeps the item but increments attempts on 'retry'", () => {
    const next = applyFlushOutcome(queue, 'c', 'retry');
    expect(next).toHaveLength(3);
    const updatedC = next.find((x) => x.id === 'c');
    expect(updatedC?.attempts).toBe(4);
  });

  it('does not mutate the input queue', () => {
    const before = JSON.stringify(queue);
    applyFlushOutcome(queue, 'b', 'sent');
    expect(JSON.stringify(queue)).toBe(before);
  });

  it('is a no-op when the item id is not in the queue', () => {
    const next = applyFlushOutcome(queue, 'nope', 'sent');
    expect(next).toHaveLength(3);
  });
});

// ─── enqueueItem ───────────────────────────────────────────────────────

describe('enqueueItem', () => {
  it('appends a new item to an empty queue', () => {
    const item = makeItem({ id: 'new', idempotencyKey: 'k1' });
    expect(enqueueItem([], item)).toEqual([item]);
  });

  it('appends a new item to a non-empty queue', () => {
    const existing = makeItem({ id: 'old', idempotencyKey: 'k1' });
    const item = makeItem({ id: 'new', idempotencyKey: 'k2' });
    const next = enqueueItem([existing], item);
    expect(next).toHaveLength(2);
    expect(next[1]?.id).toBe('new');
  });

  it('de-dups against an existing idempotency key', () => {
    const existing = makeItem({ id: 'old', idempotencyKey: 'k1' });
    const dup = makeItem({ id: 'new', idempotencyKey: 'k1' });
    const next = enqueueItem([existing], dup);
    expect(next).toHaveLength(1);
    expect(next[0]?.id).toBe('old'); // existing wins
  });

  it('does not mutate the input queue', () => {
    const queue = [makeItem({ id: 'a' })];
    const before = JSON.stringify(queue);
    enqueueItem(queue, makeItem({ id: 'b', idempotencyKey: 'k2' }));
    expect(JSON.stringify(queue)).toBe(before);
  });
});

// ─── newQueueItemId ────────────────────────────────────────────────────

describe('newQueueItemId', () => {
  it('produces a string with the q_ prefix', () => {
    const id = newQueueItemId(1700000000000, 0.5);
    expect(id.startsWith('q_')).toBe(true);
  });

  it('produces different ids for different timestamps', () => {
    const a = newQueueItemId(1700000000000, 0.5);
    const b = newQueueItemId(1700000000001, 0.5);
    expect(a).not.toBe(b);
  });

  it('produces different ids for different rand values', () => {
    const a = newQueueItemId(1700000000000, 0.1);
    const b = newQueueItemId(1700000000000, 0.9);
    expect(a).not.toBe(b);
  });
});
