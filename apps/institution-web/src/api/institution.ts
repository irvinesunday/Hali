import { apiFetch } from "./client";
import type {
  ClusterDetailResponse,
  InstitutionAreasResponse,
  InstitutionOverviewResponse,
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

export function getInstitutionSignals(
  params: GetInstitutionSignalsParams = {},
): Promise<InstitutionSignalsResponse> {
  const search = new URLSearchParams();
  if (params.areaId) search.set("areaId", params.areaId);
  if (params.state) search.set("state", params.state);
  if (params.cursor) search.set("cursor", params.cursor);
  if (typeof params.limit === "number") search.set("limit", String(params.limit));
  const query = search.toString();
  const path = query ? `/v1/institution/signals?${query}` : "/v1/institution/signals";
  return apiFetch<InstitutionSignalsResponse>(path);
}

export function getInstitutionSignal(clusterId: string): Promise<ClusterDetailResponse> {
  return apiFetch<ClusterDetailResponse>(
    `/v1/institution/signals/${encodeURIComponent(clusterId)}`,
  );
}

export function getInstitutionAreas(): Promise<InstitutionAreasResponse> {
  return apiFetch<InstitutionAreasResponse>("/v1/institution/areas");
}
