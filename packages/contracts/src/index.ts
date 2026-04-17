// @hali/contracts — framework-agnostic TypeScript types derived from
// 02_openapi.yaml. Consumed by all front-end surfaces (citizen mobile,
// institution web, institution admin web, future hali-ops web) so the
// wire contract is shared and drift-checked at the type level.
//
// Adding types: follow the OpenAPI spec in 02_openapi.yaml. Do not add
// types that do not correspond to a wire shape — types derived from
// backend internal models live in each consumer as needed.

// Client-visible feature flag exposure — matches
// ResolvedFeatureFlagsResponse in 02_openapi.yaml.
export interface ResolvedFeatureFlagsResponse {
  flags: Record<string, boolean>;
}
