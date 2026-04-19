import { apiFetch } from "./client";
import type {
  ClusterDetailResponse,
  InstitutionAcknowledgeRequest,
  InstitutionAcknowledgeResponse,
  InstitutionAreasResponse,
  InstitutionOverviewResponse,
  InstitutionRestorationQueueResponse,
  InstitutionSignalsResponse,
} from "./types";

// Typed accessors for the institution-scope backend. One function per
// route, naming mirrors the OpenAPI operationId so it's obvious which
// endpoint backs each screen. Cursor + filter params are optional so
// call sites can keep the shape focused.

export function getInstitutionOverview(): Promise<InstitutionOverviewResponse> {
  return apiFetch<InstitutionOverviewResponse>("/v1/institution/overview");
}

export interface GetInstitutionSignalsParams {
  readonly areaId?: string;
  readonly state?: string;
  readonly cursor?: string;
  readonly limit?: number;
}

// Route rename (#207): `/v1/institution/signals*` → `/v1/institution/clusters*`.
// The public contract exposes SignalCluster objects, not raw SignalEvents;
// the UI calls the new path — function names keep the `Signal` stem to
// bound blast radius across call sites (matches the backend decision).
export function getInstitutionSignals(
  params: GetInstitutionSignalsParams = {},
): Promise<InstitutionSignalsResponse> {
  const search = new URLSearchParams();
  if (params.areaId) search.set("areaId", params.areaId);
  if (params.state) search.set("state", params.state);
  if (params.cursor) search.set("cursor", params.cursor);
  if (typeof params.limit === "number") search.set("limit", String(params.limit));
  const query = search.toString();
  const path = query ? `/v1/institution/clusters?${query}` : "/v1/institution/clusters";
  return apiFetch<InstitutionSignalsResponse>(path);
}

export function getInstitutionSignal(clusterId: string): Promise<ClusterDetailResponse> {
  return apiFetch<ClusterDetailResponse>(
    `/v1/institution/clusters/${encodeURIComponent(clusterId)}`,
  );
}

export function getInstitutionAreas(): Promise<InstitutionAreasResponse> {
  return apiFetch<InstitutionAreasResponse>("/v1/institution/areas");
}

export interface GetInstitutionRestorationQueueParams {
  readonly areaId?: string;
}

// #207 Phase 4 — surfaces clusters currently in possible_restoration
// inside the caller's jurisdiction, enriched with live restoration
// vote counts so the UI shows the same evidence the lifecycle engine
// evaluates.
export function getInstitutionRestorationQueue(
  params: GetInstitutionRestorationQueueParams = {},
): Promise<InstitutionRestorationQueueResponse> {
  const search = new URLSearchParams();
  if (params.areaId) search.set("areaId", params.areaId);
  const query = search.toString();
  const path = query ? `/v1/institution/restoration?${query}` : "/v1/institution/restoration";
  return apiFetch<InstitutionRestorationQueueResponse>(path);
}

// #207 Phase 4 — explicit institution acknowledgement on a cluster in
// scope. Emits an `institution.action.recorded` outbox event; idempotent
// on `idempotencyKey` (resubmitting returns the existing record).
export function acknowledgeInstitutionCluster(
  clusterId: string,
  request: InstitutionAcknowledgeRequest,
): Promise<InstitutionAcknowledgeResponse> {
  return apiFetch<InstitutionAcknowledgeResponse>(
    `/v1/institution/clusters/${encodeURIComponent(clusterId)}/acknowledge`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}
