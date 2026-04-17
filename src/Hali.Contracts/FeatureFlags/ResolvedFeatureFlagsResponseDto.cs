using System.Collections.Generic;

namespace Hali.Contracts.FeatureFlags;

/// <summary>
/// Resolved client-visible feature flags for the current request context.
/// Only flags marked <c>ClientVisible</c> in the backend registry appear
/// here; server-only flags never leak.
///
/// The shape is intentionally a flat string→bool map. Clients do not see
/// the flag's kind, owner, targeting rules, or rollout metadata — just the
/// resolved value. See docs/arch/FEATURE_FLIGHTING_MODEL.md §4 for the
/// contract.
/// </summary>
public sealed record ResolvedFeatureFlagsResponseDto
{
    public required IReadOnlyDictionary<string, bool> Flags { get; init; }
}
