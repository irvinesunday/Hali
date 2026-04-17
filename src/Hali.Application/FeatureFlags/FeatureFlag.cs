using System;
using System.Collections.Generic;

namespace Hali.Application.FeatureFlags;

// Typed feature flag primitives. See docs/arch/FEATURE_FLIGHTING_MODEL.md for
// the policy this implements. Consumers reference flags by object (e.g.
// FeatureFlags.MobileHomeConditionBadgeEnabled) — never by raw string key — so
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
    /// <summary>
    /// Exact environment name required. Compared case-insensitively so the
    /// canonical lowercase names in the model (<c>development</c>,
    /// <c>staging</c>, <c>production</c>) match the capitalised values
    /// ASP.NET Core surfaces (<c>Development</c>, <c>Staging</c>,
    /// <c>Production</c>).
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>Institution id must be non-null and present in this set.</summary>
    public IReadOnlySet<Guid>? InstitutionIds { get; init; }

    /// <summary>Locality id must be non-null and present in this set.</summary>
    public IReadOnlySet<Guid>? LocalityIds { get; init; }

    /// <summary>
    /// Actor type must be present in this set. Compared case-insensitively
    /// so the canonical lowercase vocabulary matches regardless of how the
    /// caller sources the claim value.
    /// </summary>
    public IReadOnlySet<string>? ActorTypes { get; init; }

    /// <summary>Value returned when this rule matches.</summary>
    public bool Value { get; init; } = true;
}

/// <summary>
/// A typed boolean feature flag. Construct exactly once in
/// <see cref="FeatureFlags"/>; consumers reference the static field.
///
/// Every flag carries lifecycle metadata per
/// docs/arch/FEATURE_FLIGHTING_MODEL.md §6 so retirement policy can be
/// enforced in code. A non-permanent flag must supply an expected
/// retirement date; a permanent flag (kill switch, internal-only) must
/// not. The constructor validates this invariant up front.
/// </summary>
public sealed record BooleanFeatureFlag
{
    public BooleanFeatureFlag(
        string name,
        string description,
        string owner,
        FlagKind kind,
        FlagVisibility visibility,
        bool isPermanent,
        DateOnly? expectedRetirement,
        bool @default,
        IReadOnlyList<FlagRule> targeting)
    {
        if (isPermanent && expectedRetirement is not null)
        {
            throw new ArgumentException(
                $"Flag '{name}' is permanent and must not declare an expected retirement date.",
                nameof(expectedRetirement));
        }

        if (!isPermanent && expectedRetirement is null)
        {
            throw new ArgumentException(
                $"Flag '{name}' is not permanent and must declare an expected retirement date.",
                nameof(expectedRetirement));
        }

        Name = name;
        Description = description;
        Owner = owner;
        Kind = kind;
        Visibility = visibility;
        IsPermanent = isPermanent;
        ExpectedRetirement = expectedRetirement;
        Default = @default;
        Targeting = targeting;
    }

    public string Name { get; init; }

    public string Description { get; init; }

    public string Owner { get; init; }

    public FlagKind Kind { get; init; }

    public FlagVisibility Visibility { get; init; }

    /// <summary>
    /// True for non-expiring flags (kill switches, internal-only staff
    /// surfaces). False means <see cref="ExpectedRetirement"/> is required.
    /// </summary>
    public bool IsPermanent { get; init; }

    /// <summary>
    /// Planned retirement date for temporary flags. Must be null when
    /// <see cref="IsPermanent"/> is true.
    /// </summary>
    public DateOnly? ExpectedRetirement { get; init; }

    public bool Default { get; init; }

    public IReadOnlyList<FlagRule> Targeting { get; init; }
}

/// <summary>
/// Inputs for a single evaluation call. Construct from whatever signal the
/// caller has (HTTP context, worker job payload, or an ambient default).
/// The axis list is kept in sync with <see cref="FlagRule"/> and §3 of
/// docs/arch/FEATURE_FLIGHTING_MODEL.md.
/// </summary>
public sealed record FeatureFlagEvaluationContext(
    string Environment,
    Guid? InstitutionId,
    Guid? LocalityId,
    string ActorType);
