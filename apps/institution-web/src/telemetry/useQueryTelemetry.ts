import { useEffect, useRef } from "react";
import { ApiError } from "../api/client";
import { emitEvent, type TelemetryEventTags } from "./emit";
import type { TelemetryEventName } from "./events";

// Hook that maps a TanStack Query lifecycle onto the canonical
// `<surface>.load.started | .completed | .failed` event trio. The
// point is to keep emission contiguous across every screen — one
// started, one terminal — without each surface re-implementing a
// three-useEffect pattern (which reliably double-fires during
// Suspense / StrictMode and silently drifts per-surface).
//
// Contract:
//   - `started` emits once on first pending observation.
//   - `completed` emits once on the first success after pending.
//   - `failed` emits once on the first error after pending, tagged
//     with `status` when the error is an `ApiError` so the collector
//     can bucket 4xx / 5xx without reading the message.
//   - A refetch that flips pending→success→pending→success emits a
//     fresh started / completed pair; refetches are legitimate
//     telemetry.

export interface QueryTelemetryOptions {
  readonly startedEvent: TelemetryEventName;
  readonly completedEvent: TelemetryEventName;
  readonly failedEvent: TelemetryEventName;
  readonly isPending: boolean;
  readonly isSuccess: boolean;
  readonly isError: boolean;
  readonly error?: unknown;
  readonly extraTags?: TelemetryEventTags;
}

export function useQueryTelemetry(options: QueryTelemetryOptions): void {
  const {
    startedEvent,
    completedEvent,
    failedEvent,
    isPending,
    isSuccess,
    isError,
    error,
    extraTags,
  } = options;

  // Track the last emitted phase so we don't double-fire on re-renders.
  // "idle" after a terminal event lets a subsequent pending flip back
  // through started again (refetch path).
  const phaseRef = useRef<"idle" | "started" | "terminal">("idle");

  useEffect(() => {
    if (isPending && phaseRef.current !== "started") {
      phaseRef.current = "started";
      emitEvent(startedEvent, { ...extraTags });
      return;
    }
    if (isSuccess && phaseRef.current === "started") {
      phaseRef.current = "terminal";
      emitEvent(completedEvent, { ...extraTags });
      return;
    }
    if (isError && phaseRef.current === "started") {
      phaseRef.current = "terminal";
      const status = error instanceof ApiError ? error.status : null;
      emitEvent(failedEvent, { ...extraTags, status });
    }
  }, [
    isPending,
    isSuccess,
    isError,
    error,
    startedEvent,
    completedEvent,
    failedEvent,
    extraTags,
  ]);
}
