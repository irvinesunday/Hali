using System.Collections.Generic;

namespace Hali.Application.FeatureFlags;

/// <summary>
/// Evaluates typed feature flags against an
/// <see cref="FeatureFlagEvaluationContext"/>. Consumers pass the flag
/// object they care about — never a string key. See
/// docs/arch/FEATURE_FLIGHTING_MODEL.md for the policy.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Resolve the value of <paramref name="flag"/> for the given context.
    /// Returns the first matching rule's value, or the flag's default if
    /// no rule matches.
    /// </summary>
    bool Evaluate(BooleanFeatureFlag flag, FeatureFlagEvaluationContext context);

    /// <summary>
    /// Evaluate every <see cref="FlagVisibility.ClientVisible"/> flag for
    /// the given context and return the resolved values keyed by flag
    /// name. Server-only flags are never included in the returned map.
    /// This is the output shape consumed by the client-facing flag
    /// exposure contract.
    /// </summary>
    IReadOnlyDictionary<string, bool> EvaluateClientVisible(FeatureFlagEvaluationContext context);
}
