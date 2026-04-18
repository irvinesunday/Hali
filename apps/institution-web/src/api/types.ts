// Hand-written TypeScript shapes for the institution dashboard. These
// are a UI-focused *subset* of the full backend contract — they
// cover the fields this PR renders, not every property documented in
// `02_openapi.yaml` or `Hali.Contracts.Institutions.*` /
// `Hali.Contracts.Clusters.ClusterResponseDto`.
//
// Deliberate omissions from `ClusterDetailResponse` vs the server's
// `ClusterResponse` schema:
// - `myParticipation` — gating for the restoration CTA landing in #204
// - `restorationRatio` / `restorationYesVotes` / `restorationTotalVotes`
//   — consumed alongside the restoration CTA in #204
// Adding them now would bloat the type without a render path; they
// slot in when those PRs wire the corresponding UI.
//
// When the OpenAPI → TypeScript codegen pipeline lands, this file
// deletes in favour of generated types re-exported from
// `@hali/contracts`. Keep field names aligned with the OpenAPI
// `components/schemas` so the swap is drop-in.

export interface InstitutionOverviewSummary {
  readonly activeSignals: number;
  readonly growingSignals: number;
  readonly updatesPostedToday: number;
  readonly stabilisedToday: number;
}

export interface InstitutionArea {
  readonly id: string;
  readonly name: string;
  readonly condition: string;
  readonly activeSignals: number;
  readonly topCategory: string | null;
  readonly lastUpdatedAt: string | null;
}

export interface InstitutionOverviewResponse {
  readonly summary: InstitutionOverviewSummary;
  readonly areas: ReadonlyArray<InstitutionArea>;
}

export interface InstitutionAreasResponse {
  readonly items: ReadonlyArray<InstitutionArea>;
}

export interface InstitutionAreaRef {
  readonly id: string;
  readonly name: string;
}

export interface InstitutionSignalListItem {
  readonly id: string;
  readonly title: string;
  readonly area: InstitutionAreaRef | null;
  readonly category: string;
  readonly condition: string;
  readonly trend: string;
  readonly responseStatus: string | null;
  readonly affectedCount: number;
  readonly recentReports24h: number;
  readonly timeActiveSeconds: number;
}

export interface InstitutionSignalsResponse {
  readonly items: ReadonlyArray<InstitutionSignalListItem>;
  readonly nextCursor: string | null;
}

export type OfficialPostType = "live_update" | "scheduled_disruption" | "advisory_public_notice";

export type OfficialPostResponseStatus =
  | "acknowledged"
  | "teams_dispatched"
  | "teams_on_site"
  | "work_ongoing"
  | "restoration_in_progress"
  | "service_restored";

export type OfficialPostSeverity = "minor" | "moderate" | "major";

export type CivicCategorySlug =
  | "roads"
  | "transport"
  | "electricity"
  | "water"
  | "environment"
  | "safety"
  | "governance"
  | "infrastructure";

export interface OfficialPostResponse {
  readonly id: string;
  readonly institutionId: string;
  readonly type: OfficialPostType | string;
  readonly category: CivicCategorySlug | string;
  readonly title: string;
  readonly body: string;
  readonly startsAt: string | null;
  readonly endsAt: string | null;
  readonly status: string;
  readonly relatedClusterId: string | null;
  readonly isRestorationClaim: boolean;
  readonly createdAt: string;
  readonly responseStatus: OfficialPostResponseStatus | null;
  readonly severity: OfficialPostSeverity | null;
}

export interface OfficialPostCreateRequest {
  readonly type: OfficialPostType;
  readonly category: CivicCategorySlug | string;
  readonly title: string;
  readonly body: string;
  readonly startsAt?: string | null;
  readonly endsAt?: string | null;
  readonly relatedClusterId?: string | null;
  readonly isRestorationClaim?: boolean;
  readonly localityId?: string | null;
  readonly corridorName?: string | null;
  readonly responseStatus?: OfficialPostResponseStatus | null;
  readonly severity?: OfficialPostSeverity | null;
}

export interface ClusterDetailResponse {
  readonly id: string;
  readonly state: string;
  readonly category: string;
  readonly subcategorySlug: string | null;
  readonly title: string | null;
  readonly summary: string | null;
  readonly affectedCount: number;
  readonly observingCount: number;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly activatedAt: string | null;
  readonly possibleRestorationAt: string | null;
  readonly resolvedAt: string | null;
  readonly locationLabel: string | null;
  readonly responseStatus: string | null;
  readonly officialPosts: ReadonlyArray<OfficialPostResponse>;
}
