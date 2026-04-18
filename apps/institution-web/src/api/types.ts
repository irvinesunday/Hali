// TypeScript mirrors of the C# DTOs under `Hali.Contracts.Institutions.*`
// and `Hali.Contracts.Clusters.ClusterResponseDto`. These are
// hand-written locally because `@hali/contracts` does not yet publish
// institution types — see #202 notes and the follow-up work that will
// move these into a generated contracts package.
//
// When the OpenAPI → TypeScript codegen pipeline lands, these types
// will be deleted and re-exported from `@hali/contracts`. Keep the
// shape aligned with `02_openapi.yaml` paths under `/v1/institution/*`
// and `GET /v1/clusters/{id}` so the swap is drop-in.

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
}
