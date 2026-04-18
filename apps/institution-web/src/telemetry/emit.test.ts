import { afterEach, describe, expect, it, vi } from "vitest";
import {
  emitEvent,
  resetTelemetryTransport,
  setTelemetryTransport,
  type TelemetryEventRecord,
} from "./emit";
import { TelemetryEvents } from "./events";

describe("emitEvent", () => {
  afterEach(() => {
    resetTelemetryTransport();
  });

  it("dispatches the event name + tags through the active transport", () => {
    const transport = vi.fn();
    setTelemetryTransport(transport);

    emitEvent(TelemetryEvents.OverviewLoadStarted, { route_name: "overview" });

    expect(transport).toHaveBeenCalledTimes(1);
    const record = transport.mock.calls[0]?.[0] as TelemetryEventRecord;
    expect(record.name).toBe(TelemetryEvents.OverviewLoadStarted);
    expect(record.tags).toEqual({ route_name: "overview" });
    expect(typeof record.timestamp).toBe("number");
  });

  it("freezes the tags object so transports cannot mutate it", () => {
    const transport = vi.fn();
    setTelemetryTransport(transport);

    emitEvent(TelemetryEvents.AuthScopeDenied, { route_template: "/v1/x", method: "GET" });

    const record = transport.mock.calls[0]?.[0] as TelemetryEventRecord;
    expect(Object.isFrozen(record.tags)).toBe(true);
  });

  it("never throws when the transport throws", () => {
    setTelemetryTransport(() => {
      throw new Error("kaboom");
    });

    expect(() => emitEvent(TelemetryEvents.SignalsBoardLoadStarted)).not.toThrow();
  });

  it("defaults tags to an empty object when none provided", () => {
    const transport = vi.fn();
    setTelemetryTransport(transport);

    emitEvent(TelemetryEvents.OfficialPostDraftCancelled);

    const record = transport.mock.calls[0]?.[0] as TelemetryEventRecord;
    expect(record.tags).toEqual({});
  });

  it("resetTelemetryTransport restores the default transport", () => {
    const transport = vi.fn();
    setTelemetryTransport(transport);
    resetTelemetryTransport();

    emitEvent(TelemetryEvents.OverviewLoadCompleted);

    expect(transport).not.toHaveBeenCalled();
  });
});
