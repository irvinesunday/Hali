// apps/citizen-mobile/src/lib/offlineQueueBootstrap.ts
//
// Wires the runtime offlineQueue (src/lib/offlineQueue.ts) to the API
// service layer. The queue itself is endpoint-agnostic — this file is
// the only place that knows how to dispatch a QueueItem.kind to the
// right service function.
//
// Called once from (app)/_layout.tsx via initOfflineQueue().

import { offlineQueue } from './offlineQueue';
import type {
  OfflineQueueKind,
  QueueItem,
} from '../utils/offlineQueueLogic';
import { submitSignal } from '../api/signals';
import {
  participate,
  addContext,
  submitRestorationResponse,
} from '../api/clusters';
import { participationTypeToBackend } from '../utils/participationApi';
import type {
  ApiError,
  ParticipationRequest,
  Result,
  SignalSubmitRequest,
  ContextRequest,
  RestorationResponseRequest,
} from '../types/api';

/**
 * Extract the cluster id from a queued participation/context/restoration
 * endpoint path. Pure helper.
 *   '/v1/clusters/abc-123/participation' → 'abc-123'
 */
function clusterIdFromEndpoint(endpoint: string): string {
  const match = endpoint.match(/\/v1\/clusters\/([^/]+)\//);
  return match?.[1] ?? '';
}

/**
 * Dispatch a single queued item back to the appropriate service function.
 * Returns a Result so the queue can decide whether to retry/drop/keep.
 *
 * The queue stored the body with the idempotency key already injected,
 * so retries reuse the same key automatically.
 */
async function dispatch(item: QueueItem): Promise<Result<unknown, ApiError>> {
  switch (item.kind as OfflineQueueKind) {
    case 'signal_submit':
      return submitSignal(item.body as unknown as SignalSubmitRequest);

    case 'participation': {
      // Body was queued with the mobile snake_case `type` AND the
      // PascalCase server form already injected. We re-coerce here so
      // we don't depend on which form was queued.
      const body = item.body as Record<string, unknown> & {
        type?: string;
        deviceHash?: string;
        idempotencyKey?: string;
      };

      // If the queued body has a snake_case type, convert it via the
      // shared helper. Otherwise it's already in the wire form.
      const type =
        body.type === 'affected' ||
        body.type === 'observing' ||
        body.type === 'no_longer_affected'
          ? body.type
          : undefined;

      if (type !== undefined) {
        return participate(clusterIdFromEndpoint(item.endpoint), {
          type,
          deviceHash: body.deviceHash ?? '',
          idempotencyKey: body.idempotencyKey,
        } as ParticipationRequest);
      }
      // Already-wire-form fallback: send as-is via a direct apiRequest.
      // We re-route through participate() to get the converter for free
      // by mapping the wire string back to the mobile union.
      const reverseMap: Record<string, ParticipationRequest['type']> = {
        Affected: 'affected',
        Observing: 'observing',
        NoLongerAffected: 'no_longer_affected',
      };
      const wireType = body.type ?? '';
      const mobileType = reverseMap[wireType] ?? 'affected';
      return participate(clusterIdFromEndpoint(item.endpoint), {
        type: mobileType,
        deviceHash: body.deviceHash ?? '',
        idempotencyKey: body.idempotencyKey,
      });
    }

    case 'restoration_response':
      return submitRestorationResponse(
        clusterIdFromEndpoint(item.endpoint),
        item.body as unknown as RestorationResponseRequest,
      );

    case 'context':
      return addContext(
        clusterIdFromEndpoint(item.endpoint),
        item.body as unknown as ContextRequest,
      );
  }
}

// Suppress the unused-import warning for the converter — referenced
// implicitly by participate() inside dispatch.
void participationTypeToBackend;

/**
 * Initialise the offline queue runtime. Idempotent.
 */
export async function initOfflineQueue(): Promise<void> {
  await offlineQueue.init(dispatch);
}
