using System;
using System.Collections.Generic;

namespace Hali.Application.FeatureFlags;

// Typed feature flag primitives. See docs/arch/FEATURE_FLIGHTING_MODEL.md for
// the policy this implements. Consumers reference flags by object (e.g.
// FeatureFlags.MobileConditionBadgeEnabled) — never by raw string key — so
// the typed-not-stringly principle in §1 of the model is enforced by the
// type system.

public enum FlagKind
{
    /// <summary>Feature deployable but not yet taking traffic.</summary>
    DarkLaunch,

    /// <summary>Feature scoped to a specific audience before full rollout.</summary>
    Pilot,

    /// <summary>Permanent switch to disable a non-core code path under load or failure.</summary>
    KillSwitch,

    /// <summary>Surface that must only ever appear to internal / ops actors.</summary>
    InternalOnly
}

public enum FlagVisibility
{
    /// <summary>Resolved value must never be exposed to any client.</summary>
    ServerOnly,

    /// <summary>Resolved value is eligible for exposure via the client-safe flag endpoint.</summary>
    ClientVisible
}

/// <summary>
/// One evaluation rule. Rules are checked in declaration order; the first
/// rule whose non-null constraints all match returns its <see cref="Value"/>.
/// If no rule matches, the flag's <see cref="BooleanFeatureFlag.Default"/>
/// applies.
///
/// A null constraint on a given axis means "don't care" for that axis; a
/// non-null constraint must be satisfied for the rule to match.
/// </summary>
public sealed record FlagRule
{
    /// <summary>Exact environment name required (e.g. "Development", "Production").</summary>
    public string? Environment { get; init; }

    /// <summary>Institution id must be non-null and present in this set.</summary>
    public IReadOnlySet<Guid>? InstitutionIds { get; init; }

    /// <summary>Locality id must be non-null and present in this set.</summary>
    public IReadOnlySet<Guid>? LocalityIds { get; init; }

    /// <summary>Actor type must be present in this set.</summary>
    public IReadOnlySet<string>? ActorTypes { get; init; }

    /// <summary>Value returned when this rule matches.</summary>
    public bool Value { get; init; } = true;
}

/// <summary>
/// A typed boolean feature flag. Construct exactly once in
/// <see cref="FeatureFlags"/>; consumers reference the static field.
/// </summary>
public sealed record BooleanFeatureFlag(
    string Name,
    string Description,
    string Owner,
    FlagKind Kind,
    FlagVisibility Visibility,
    bool Default,
    IReadOnlyList<FlagRule> Targeting);

/// <summary>
/// Inputs for a single evaluation call. Construct from whatever signal the
/// caller has (HTTP context, worker job payload, or an ambient default).
/// Keep the axis list exactly in sync with
/// <see cref="FlagRule"/> and
/// <see cref="FEATURE_FLIGHTING_MODEL"/> §3.
/// </summary>
public sealed record FeatureFlagEvaluationContext(
    string Environment,
    Guid? InstitutionId,
    Guid? LocalityId,
    string ActorType);
