import { render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import {
  resetTelemetryTransport,
  setTelemetryTransport,
  type TelemetryEventRecord,
  type TelemetryTransport,
} from "./emit";
import { TelemetryEvents } from "./events";
import { useQueryTelemetry, type QueryTelemetryOptions } from "./useQueryTelemetry";

function Probe(props: QueryTelemetryOptions): null {
  useQueryTelemetry(props);
  return null;
}

describe("useQueryTelemetry", () => {
  let transport: ReturnType<typeof vi.fn> & TelemetryTransport;

  beforeEach(() => {
    transport = vi.fn() as ReturnType<typeof vi.fn> & TelemetryTransport;
    setTelemetryTransport(transport);
  });

  afterEach(() => {
    resetTelemetryTransport();
  });

  const baseOptions: QueryTelemetryOptions = {
    startedEvent: TelemetryEvents.OverviewLoadStarted,
    completedEvent: TelemetryEvents.OverviewLoadCompleted,
    failedEvent: TelemetryEvents.OverviewLoadFailed,
    isPending: false,
    isSuccess: false,
    isError: false,
  };

  function names(): string[] {
    return transport.mock.calls.map((c) => (c[0] as TelemetryEventRecord).name);
  }

  it("emits started → completed for a pending → success transition", () => {
    const { rerender } = render(<Probe {...baseOptions} isPending={true} />);
    expect(names()).toEqual([TelemetryEvents.OverviewLoadStarted]);

    rerender(<Probe {...baseOptions} isPending={false} isSuccess={true} />);
    expect(names()).toEqual([
      TelemetryEvents.OverviewLoadStarted,
      TelemetryEvents.OverviewLoadCompleted,
    ]);
  });

  it("emits started → failed with a status tag for ApiError", () => {
    const { rerender } = render(<Probe {...baseOptions} isPending={true} />);
    rerender(
      <Probe
        {...baseOptions}
        isPending={false}
        isError={true}
        error={new ApiError(403, "/v1/x", "denied")}
      />,
    );

    const records = transport.mock.calls.map((c) => c[0] as TelemetryEventRecord);
    expect(records.map((r) => r.name)).toEqual([
      TelemetryEvents.OverviewLoadStarted,
      TelemetryEvents.OverviewLoadFailed,
    ]);
    expect(records[1]?.tags).toMatchObject({ status: 403 });
  });

  it("emits status: null for non-ApiError failures", () => {
    const { rerender } = render(<Probe {...baseOptions} isPending={true} />);
    rerender(
      <Probe {...baseOptions} isPending={false} isError={true} error={new Error("network")} />,
    );

    const failedRecord = transport.mock.calls[1]?.[0] as TelemetryEventRecord;
    expect(failedRecord.name).toBe(TelemetryEvents.OverviewLoadFailed);
    expect(failedRecord.tags).toMatchObject({ status: null });
  });

  it("does not double-fire started across re-renders while pending", () => {
    const { rerender } = render(<Probe {...baseOptions} isPending={true} />);
    rerender(<Probe {...baseOptions} isPending={true} />);
    rerender(<Probe {...baseOptions} isPending={true} />);
    expect(names()).toEqual([TelemetryEvents.OverviewLoadStarted]);
  });

  it("emits a fresh started/completed pair on a refetch", () => {
    const { rerender } = render(<Probe {...baseOptions} isPending={true} />);
    rerender(<Probe {...baseOptions} isPending={false} isSuccess={true} />);
    // Refetch begins.
    rerender(<Probe {...baseOptions} isPending={true} />);
    rerender(<Probe {...baseOptions} isPending={false} isSuccess={true} />);

    expect(names()).toEqual([
      TelemetryEvents.OverviewLoadStarted,
      TelemetryEvents.OverviewLoadCompleted,
      TelemetryEvents.OverviewLoadStarted,
      TelemetryEvents.OverviewLoadCompleted,
    ]);
  });

  it("merges extraTags into started + terminal events", () => {
    const { rerender } = render(
      <Probe {...baseOptions} isPending={true} extraTags={{ surface: "overview" }} />,
    );
    rerender(
      <Probe
        {...baseOptions}
        isPending={false}
        isSuccess={true}
        extraTags={{ surface: "overview" }}
      />,
    );

    const records = transport.mock.calls.map((c) => c[0] as TelemetryEventRecord);
    expect(records[0]?.tags).toMatchObject({ surface: "overview" });
    expect(records[1]?.tags).toMatchObject({ surface: "overview" });
  });
});
