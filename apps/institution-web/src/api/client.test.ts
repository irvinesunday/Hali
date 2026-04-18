import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  resetTelemetryTransport,
  setTelemetryTransport,
  type TelemetryEventRecord,
} from "../telemetry/emit";
import { TelemetryEvents } from "../telemetry/events";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../test/mockFetch";
import { ApiError, apiFetch } from "./client";

describe("apiFetch", () => {
  let transport: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    transport = vi.fn();
    setTelemetryTransport(transport);
  });

  afterEach(() => {
    resetTelemetryTransport();
    restoreFetch();
  });

  it("returns parsed JSON for a 2xx response", async () => {
    mockFetch({
      "/v1/ping": () => jsonResponse({ ok: true }),
    });

    const result = await apiFetch<{ ok: boolean }>("/v1/ping");
    expect(result).toEqual({ ok: true });
  });

  it("throws ApiError for non-2xx and does not emit auth.scope.denied", async () => {
    mockFetch({
      "/v1/ping": () => errorResponse(500, "boom"),
    });

    await expect(apiFetch("/v1/ping")).rejects.toBeInstanceOf(ApiError);
    const names = transport.mock.calls.map((c) => (c[0] as TelemetryEventRecord).name);
    expect(names).not.toContain(TelemetryEvents.AuthScopeDenied);
  });

  it("emits auth.scope.denied with a bounded route_template on 403", async () => {
    mockFetch({
      "/v1/institution/signals/3fa85f64-5717-4562-b3fc-2c963f66afa6": () =>
        errorResponse(403, "denied"),
    });

    await expect(
      apiFetch("/v1/institution/signals/3fa85f64-5717-4562-b3fc-2c963f66afa6"),
    ).rejects.toBeInstanceOf(ApiError);

    const denied = transport.mock.calls
      .map((c) => c[0] as TelemetryEventRecord)
      .find((r) => r.name === TelemetryEvents.AuthScopeDenied);
    expect(denied).toBeDefined();
    expect(denied?.tags).toMatchObject({
      route_template: "/v1/institution/signals/:id",
      method: "GET",
    });
  });

  it("strips query strings from the route template", async () => {
    mockFetch({
      "/v1/institution/signals": () => errorResponse(403, "denied"),
    });

    await expect(
      apiFetch("/v1/institution/signals?cursor=abc"),
    ).rejects.toBeInstanceOf(ApiError);

    const denied = transport.mock.calls
      .map((c) => c[0] as TelemetryEventRecord)
      .find((r) => r.name === TelemetryEvents.AuthScopeDenied);
    expect(denied?.tags).toMatchObject({ route_template: "/v1/institution/signals" });
  });

  it("tags the method as POST when the caller specifies a method", async () => {
    mockFetch({
      "/v1/official-posts": () => errorResponse(403, "denied"),
    });

    await expect(
      apiFetch("/v1/official-posts", { method: "POST" }),
    ).rejects.toBeInstanceOf(ApiError);

    const denied = transport.mock.calls
      .map((c) => c[0] as TelemetryEventRecord)
      .find((r) => r.name === TelemetryEvents.AuthScopeDenied);
    expect(denied?.tags).toMatchObject({ method: "POST" });
  });
});
