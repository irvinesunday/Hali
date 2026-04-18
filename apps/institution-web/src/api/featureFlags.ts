import { apiFetch } from "./client";

// Client-visible feature flags resolved for the current request
// context. The backend endpoint (`GET /v1/feature-flags`, see
// `FeatureFlagsController`) returns a flat string→bool map containing
// only flags declared as `ClientVisible`; server-only flags
// (including the workers push dispatcher kill switch) never leak.
//
// The endpoint accepts `Accept: application/json`, is anonymous-safe
// (falls back to `actor_type = "anonymous"`), and optionally takes a
// `localityId` query string for locality-targeted rollouts. The
// institution shell doesn't know a locality yet (no locality switcher
// in this PR), so the default call is unscoped.

export interface ResolvedFeatureFlags {
  readonly flags: Readonly<Record<string, boolean>>;
}

export function getResolvedFeatureFlags(): Promise<ResolvedFeatureFlags> {
  return apiFetch<ResolvedFeatureFlags>("/v1/feature-flags");
}

// Typed accessors for the institution dashboard's known flag keys.
// Keeping them in one place means adding a new institution flag is
// one spot in this file plus the Rust backend catalog — never a raw
// string spread across screens. Mirrors the "typed-not-stringly"
// rule in docs/arch/FEATURE_FLIGHTING_MODEL.md §1.
export const InstitutionWebFlagKeys = {
  enabled: "institution_web.enabled",
  postUpdateEnabled: "institution_web.post_update.enabled",
  restorationClaimEnabled: "institution_web.restoration_claim.enabled",
} as const;

export type InstitutionWebFlagKey =
  (typeof InstitutionWebFlagKeys)[keyof typeof InstitutionWebFlagKeys];
