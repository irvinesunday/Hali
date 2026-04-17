using System.Collections.Generic;

namespace Hali.Application.FeatureFlags;

/// <summary>
/// In-process feature flag evaluator. Stateless — registered as a
/// singleton. If a DB-backed registry is ever needed, the interface
/// stays the same and the implementation moves to Infrastructure.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    public bool Evaluate(BooleanFeatureFlag flag, FeatureFlagEvaluationContext context)
    {
        foreach (FlagRule rule in flag.Targeting)
        {
            if (Matches(rule, context))
            {
                return rule.Value;
            }
        }

        return flag.Default;
    }

    public IReadOnlyDictionary<string, bool> EvaluateClientVisible(FeatureFlagEvaluationContext context)
    {
        Dictionary<string, bool> result = new(capacity: FeatureFlags.All.Count);
        foreach (BooleanFeatureFlag flag in FeatureFlags.All)
        {
            if (flag.Visibility == FlagVisibility.ClientVisible)
            {
                result[flag.Name] = Evaluate(flag, context);
            }
        }

        return result;
    }

    private static bool Matches(FlagRule rule, FeatureFlagEvaluationContext context)
    {
        if (rule.Environment is not null && !string.Equals(rule.Environment, context.Environment, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (rule.InstitutionIds is not null)
        {
            if (context.InstitutionId is null || !rule.InstitutionIds.Contains(context.InstitutionId.Value))
            {
                return false;
            }
        }

        if (rule.LocalityIds is not null)
        {
            if (context.LocalityId is null || !rule.LocalityIds.Contains(context.LocalityId.Value))
            {
                return false;
            }
        }

        if (rule.ActorTypes is not null && !rule.ActorTypes.Contains(context.ActorType))
        {
            return false;
        }

        return true;
    }
}
