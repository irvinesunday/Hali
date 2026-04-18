import type { TelemetryEventName } from "./events";

// Thin emit function + pluggable transport for institution-web
// telemetry. The point of this module is to satisfy
// `docs/arch/OBSERVABILITY_MODEL.md` §5 (institution-web instruments
// every shell / load / write / scope-denied surface from day one)
// without coupling to a specific backend — the unified front-end
// crash + event pipeline lands in #208.
//
// Until then the default transport is a no-op in production and a
// labelled console.debug in development, which keeps emission
// side-effect-free and lets ops flip to a real transport later
// without touching every call site.
//
// Emission MUST NOT throw back into feature code. A broken transport
// must never break a UI flow.

export type TelemetryTagValue = string | number | boolean | null;

export interface TelemetryEventTags {
  readonly [key: string]: TelemetryTagValue | undefined;
}

export interface TelemetryEventRecord {
  readonly name: TelemetryEventName;
  readonly tags: TelemetryEventTags;
  readonly timestamp: number;
}

export type TelemetryTransport = (event: TelemetryEventRecord) => void;

const isDevMode: boolean =
  typeof import.meta !== "undefined" && Boolean(import.meta.env?.DEV);

function consoleTransport(event: TelemetryEventRecord): void {
  if (!isDevMode) return;
  if (typeof console === "undefined" || typeof console.debug !== "function") return;
  console.debug("[telemetry]", event.name, event.tags);
}

let activeTransport: TelemetryTransport = consoleTransport;

export function emitEvent(
  name: TelemetryEventName,
  tags: TelemetryEventTags = {},
): void {
  const record: TelemetryEventRecord = {
    name,
    tags: Object.freeze({ ...tags }),
    timestamp: Date.now(),
  };
  try {
    activeTransport(record);
  } catch (err) {
    if (isDevMode && typeof console !== "undefined" && typeof console.warn === "function") {
      console.warn("[telemetry] transport failed", err);
    }
  }
}

export function setTelemetryTransport(fn: TelemetryTransport): void {
  activeTransport = fn;
}

export function resetTelemetryTransport(): void {
  activeTransport = consoleTransport;
}
