// apps/citizen-mobile/src/lib/offlineQueue.ts
//
// Offline write queue runtime.
//
// Responsibilities:
//   - Persist queued writes to AsyncStorage so they survive app restart
//   - Listen for connectivity changes (NetInfo) and app foreground
//     (AppState) to trigger flush
//   - Replay each queued item against the registered submit function
//     and apply the flush outcome from offlineQueueLogic
//
// Design boundaries:
//   - This file does NOT know how to call specific endpoints. It receives
//     a `submit` callback when initialised, and that callback is the only
//     bridge to the API service layer.
//   - Pure logic (classification, queue mutation, idempotency keys) lives
//     in src/utils/offlineQueueLogic.ts and src/utils/idempotency.ts so it
//     can be unit tested without AsyncStorage / NetInfo / AppState mocks.
//
// Storage shape:
//   AsyncStorage key 'hali_offline_queue_v1' → JSON array of QueueItem.

import AsyncStorage from '@react-native-async-storage/async-storage';
import NetInfo from '@react-native-community/netinfo';
import { AppState, type AppStateStatus } from 'react-native';
import {
  applyFlushOutcome,
  classifyFlushResult,
  enqueueItem,
  newQueueItemId,
  type OfflineQueueKind,
  type QueueItem,
} from '../utils/offlineQueueLogic';
import { generateIdempotencyKey } from '../utils/idempotency';
import type { ApiError, Result } from '../types/api';

const STORAGE_KEY = 'hali_offline_queue_v1';

/**
 * Submit callback shape: given a queued item, attempt to send it and
 * return a Result. The runtime doesn't care which endpoint is hit —
 * it only inspects Result.ok / Result.error.status.
 */
export type SubmitFn = (
  item: QueueItem,
) => Promise<Result<unknown, ApiError>>;

/**
 * Lifecycle events the queue emits to subscribers (e.g. a toast layer).
 */
export type QueueEvent =
  | { type: 'enqueued'; item: QueueItem }
  | { type: 'flushed'; itemId: string }
  | { type: 'dropped'; itemId: string; error: ApiError }
  | { type: 'retry'; itemId: string; error: ApiError };

export type QueueListener = (event: QueueEvent) => void;

class OfflineQueue {
  private items: QueueItem[] = [];
  private loaded = false;
  private flushing = false;
  private submitFn: SubmitFn | null = null;
  private listeners = new Set<QueueListener>();
  private netInfoUnsubscribe: (() => void) | null = null;
  private appStateSubscription: { remove: () => void } | null = null;

  /**
   * Initialise the queue. Wires up NetInfo + AppState listeners and
   * loads the persisted items from AsyncStorage. Idempotent — calling
   * twice does nothing.
   */
  async init(submit: SubmitFn): Promise<void> {
    if (this.loaded) return;
    this.submitFn = submit;
    await this.load();
    this.loaded = true;

    // Listen for connectivity changes
    this.netInfoUnsubscribe = NetInfo.addEventListener((state) => {
      if (state.isConnected === true) {
        void this.flush();
      }
    });

    // Listen for app foreground
    this.appStateSubscription = AppState.addEventListener(
      'change',
      (nextState: AppStateStatus) => {
        if (nextState === 'active') {
          void this.flush();
        }
      },
    );
  }

  /**
   * Tear down listeners. Useful for tests and clean shutdowns.
   */
  shutdown(): void {
    this.netInfoUnsubscribe?.();
    this.netInfoUnsubscribe = null;
    this.appStateSubscription?.remove();
    this.appStateSubscription = null;
    this.listeners.clear();
    this.submitFn = null;
    this.loaded = false;
    this.items = [];
  }

  /**
   * Subscribe to queue lifecycle events. Returns an unsubscribe function.
   */
  subscribe(listener: QueueListener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /**
   * Snapshot of the current queue (read-only). For diagnostics / UI.
   */
  getItems(): readonly QueueItem[] {
    return this.items;
  }

  /**
   * Enqueue a write. Generates the idempotency key from the body BEFORE
   * the item lands in the queue, so any flush retry reuses the same key.
   *
   * Returns the QueueItem that was actually enqueued (or the existing one
   * if a duplicate idempotency key was already in the queue).
   */
  async enqueue(
    kind: OfflineQueueKind,
    endpoint: string,
    body: Record<string, unknown>,
  ): Promise<QueueItem> {
    const idempotencyKey = await generateIdempotencyKey(endpoint, body);

    // De-dup against existing items
    const existing = this.items.find(
      (item) => item.idempotencyKey === idempotencyKey,
    );
    if (existing) return existing;

    const item: QueueItem = {
      id: newQueueItemId(Date.now(), Math.random()),
      kind,
      endpoint,
      // Inject the idempotency key into the body itself, mirroring the
      // backend's expectation for /v1/signals/submit and the participation
      // endpoint (both accept idempotencyKey as a body field).
      body: { ...body, idempotencyKey },
      idempotencyKey,
      createdAt: new Date().toISOString(),
      attempts: 0,
    };

    this.items = enqueueItem(this.items, item);
    await this.persist();
    this.emit({ type: 'enqueued', item });
    return item;
  }

  /**
   * Attempt to flush every queued item once. Items that retry stay in
   * the queue; items that send or are dropped are removed. Reentrancy
   * is protected — a second flush() call while one is in progress does
   * nothing.
   */
  async flush(): Promise<void> {
    if (this.flushing || !this.loaded || this.submitFn === null) return;
    if (this.items.length === 0) return;
    this.flushing = true;

    try {
      // Snapshot the queue at the start of this flush so concurrent
      // enqueues during the loop don't get processed twice.
      const snapshot = [...this.items];

      for (const item of snapshot) {
        // Item may have been removed by a parallel enqueue dedup —
        // double check before processing.
        if (!this.items.some((q) => q.id === item.id)) continue;

        const submit = this.submitFn;
        if (submit === null) break;

        let result: Result<unknown, ApiError>;
        try {
          result = await submit(item);
        } catch {
          result = {
            ok: false,
            error: {
              status: 0,
              code: 'network_error',
              message: 'Submit threw — treating as network error.',
            },
          };
        }

        const outcome = classifyFlushResult(result);
        this.items = applyFlushOutcome(this.items, item.id, outcome);

        if (outcome === 'sent') {
          this.emit({ type: 'flushed', itemId: item.id });
        } else if (outcome === 'dropped' && !result.ok) {
          this.emit({
            type: 'dropped',
            itemId: item.id,
            error: result.error,
          });
        } else if (outcome === 'retry' && !result.ok) {
          this.emit({
            type: 'retry',
            itemId: item.id,
            error: result.error,
          });
        }
      }

      await this.persist();
    } finally {
      this.flushing = false;
    }
  }

  // ── Persistence ────────────────────────────────────────────────────

  private async load(): Promise<void> {
    try {
      const raw = await AsyncStorage.getItem(STORAGE_KEY);
      if (raw === null) {
        this.items = [];
        return;
      }
      const parsed: unknown = JSON.parse(raw);
      if (Array.isArray(parsed)) {
        // Trust shape narrowly: filter to objects with the required fields.
        this.items = parsed.filter(isQueueItem);
      } else {
        this.items = [];
      }
    } catch {
      this.items = [];
    }
  }

  private async persist(): Promise<void> {
    try {
      await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify(this.items));
    } catch {
      // Persistence failure is non-fatal — the queue continues in memory
      // and the next persist() call will retry.
    }
  }

  // ── Events ─────────────────────────────────────────────────────────

  private emit(event: QueueEvent): void {
    for (const listener of this.listeners) {
      try {
        listener(event);
      } catch {
        // Listener errors must not break the queue
      }
    }
  }
}

// ─── Type guard ────────────────────────────────────────────────────────

function isQueueItem(value: unknown): value is QueueItem {
  if (value === null || typeof value !== 'object') return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.id === 'string' &&
    typeof v.kind === 'string' &&
    typeof v.endpoint === 'string' &&
    typeof v.idempotencyKey === 'string' &&
    typeof v.createdAt === 'string' &&
    typeof v.attempts === 'number' &&
    typeof v.body === 'object' &&
    v.body !== null
  );
}

// ─── Singleton ─────────────────────────────────────────────────────────
//
// Single instance per app. The queue's identity is its persisted
// AsyncStorage state — multiple instances would race on writes.

export const offlineQueue = new OfflineQueue();
