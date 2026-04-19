import { apiFetch } from "./client";
import type { OfficialPostCreateRequest, OfficialPostResponse } from "./types";

// Typed accessors for POST /v1/institution/official-updates (#207 route
// rename — was /v1/official-posts). The backend derives institution_id
// from the JWT claim, so the UI never sends it. The caller attaches
// `relatedClusterId` when posting from a cluster detail; otherwise the
// post is cluster-less and is scoped by `localityId` / `corridorName`.

export function createOfficialPost(
  request: OfficialPostCreateRequest,
): Promise<OfficialPostResponse> {
  return apiFetch<OfficialPostResponse>("/v1/institution/official-updates", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}
