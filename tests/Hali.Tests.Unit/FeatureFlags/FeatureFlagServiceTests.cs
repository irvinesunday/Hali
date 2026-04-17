using System;
using System.Collections.Generic;
using Hali.Application.FeatureFlags;
using Xunit;

namespace Hali.Tests.Unit.FeatureFlags;

public class FeatureFlagServiceTests
{
    private readonly FeatureFlagService _service = new();

    private static BooleanFeatureFlag MakeFlag(
        string name = "test.flag.enabled",
        FlagVisibility visibility = FlagVisibility.ServerOnly,
        bool @default = true,
        IReadOnlyList<FlagRule>? targeting = null,
        FlagKind kind = FlagKind.DarkLaunch,
        bool isPermanent = false)
    {
        return new BooleanFeatureFlag(
            name: name,
            description: "test",
            owner: "@test",
            kind: kind,
            visibility: visibility,
            isPermanent: isPermanent,
            expectedRetirement: isPermanent ? null : new DateOnly(2099, 1, 1),
            @default: @default,
            targeting: targeting ?? new List<FlagRule>());
    }

    private static FeatureFlagEvaluationContext MakeContext(
        string environment = "Production",
        Guid? institutionId = null,
        Guid? localityId = null,
        string actorType = "citizen")
    {
        return new FeatureFlagEvaluationContext(environment, institutionId, localityId, actorType);
    }

    [Fact]
    public void Evaluate_NoRules_ReturnsDefault()
    {
        BooleanFeatureFlag flag = MakeFlag(@default: true);
        Assert.True(_service.Evaluate(flag, MakeContext()));
    }

    [Fact]
    public void Evaluate_EnvironmentRule_Matches()
    {
        BooleanFeatureFlag flag = MakeFlag(
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { Environment = "Development", Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(environment: "Development")));
        Assert.False(_service.Evaluate(flag, MakeContext(environment: "Production")));
    }

    [Fact]
    public void Evaluate_EnvironmentRule_CaseInsensitive()
    {
        // Rule written with canonical lowercase vocabulary should still match
        // a context carrying ASP.NET Core's capitalised environment name.
        BooleanFeatureFlag flag = MakeFlag(
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { Environment = "development", Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(environment: "Development")));
        Assert.True(_service.Evaluate(flag, MakeContext(environment: "DEVELOPMENT")));
    }

    [Fact]
    public void Evaluate_InstitutionIdRule_Matches()
    {
        Guid targetInstitution = Guid.NewGuid();
        BooleanFeatureFlag flag = MakeFlag(
            visibility: FlagVisibility.ClientVisible,
            kind: FlagKind.Pilot,
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { InstitutionIds = new HashSet<Guid> { targetInstitution }, Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(institutionId: targetInstitution)));
        Assert.False(_service.Evaluate(flag, MakeContext(institutionId: Guid.NewGuid())));
        // Rule requires institution id; context has none → no match → default.
        Assert.False(_service.Evaluate(flag, MakeContext()));
    }

    [Fact]
    public void Evaluate_LocalityIdRule_Matches()
    {
        Guid targetLocality = Guid.NewGuid();
        BooleanFeatureFlag flag = MakeFlag(
            visibility: FlagVisibility.ClientVisible,
            kind: FlagKind.Pilot,
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { LocalityIds = new HashSet<Guid> { targetLocality }, Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(localityId: targetLocality)));
        Assert.False(_service.Evaluate(flag, MakeContext(localityId: Guid.NewGuid())));
        Assert.False(_service.Evaluate(flag, MakeContext()));
    }

    [Fact]
    public void Evaluate_ActorTypeRule_Matches()
    {
        BooleanFeatureFlag flag = MakeFlag(
            kind: FlagKind.InternalOnly,
            isPermanent: true,
            visibility: FlagVisibility.ClientVisible,
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { ActorTypes = new HashSet<string> { "admin" }, Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(actorType: "admin")));
        Assert.False(_service.Evaluate(flag, MakeContext(actorType: "citizen")));
    }

    [Fact]
    public void Evaluate_ActorTypeRule_CaseInsensitive()
    {
        // Rule written with lowercase canonical vocabulary matches context
        // values regardless of casing (e.g. JWT role claim variants).
        BooleanFeatureFlag flag = MakeFlag(
            kind: FlagKind.InternalOnly,
            isPermanent: true,
            visibility: FlagVisibility.ClientVisible,
            @default: false,
            targeting: new List<FlagRule>
            {
                new() { ActorTypes = new HashSet<string> { "admin" }, Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(actorType: "Admin")));
        Assert.True(_service.Evaluate(flag, MakeContext(actorType: "ADMIN")));
        Assert.False(_service.Evaluate(flag, MakeContext(actorType: "citizen")));
    }

    [Fact]
    public void Evaluate_MultipleRules_FirstMatchWins()
    {
        BooleanFeatureFlag flag = MakeFlag(
            kind: FlagKind.Pilot,
            @default: false,
            targeting: new List<FlagRule>
            {
                // First rule: Development → false (opt out)
                new() { Environment = "Development", Value = false },
                // Second rule: everyone else → true
                new() { Value = true },
            });

        Assert.False(_service.Evaluate(flag, MakeContext(environment: "Development")));
        Assert.True(_service.Evaluate(flag, MakeContext(environment: "Production")));
    }

    [Fact]
    public void Evaluate_CombinedConstraints_AllMustMatch()
    {
        Guid targetInstitution = Guid.NewGuid();
        BooleanFeatureFlag flag = MakeFlag(
            visibility: FlagVisibility.ClientVisible,
            kind: FlagKind.Pilot,
            @default: false,
            targeting: new List<FlagRule>
            {
                new()
                {
                    Environment = "Production",
                    InstitutionIds = new HashSet<Guid> { targetInstitution },
                    Value = true,
                },
            });

        // Both must match.
        Assert.True(_service.Evaluate(flag, MakeContext(environment: "Production", institutionId: targetInstitution)));
        // Environment matches but institution doesn't.
        Assert.False(_service.Evaluate(flag, MakeContext(environment: "Production", institutionId: Guid.NewGuid())));
        // Institution matches but environment doesn't.
        Assert.False(_service.Evaluate(flag, MakeContext(environment: "Development", institutionId: targetInstitution)));
    }

    [Fact]
    public void EvaluateClientVisible_ReturnsOnlyClientVisibleFlags()
    {
        IReadOnlyDictionary<string, bool> resolved = _service.EvaluateClientVisible(MakeContext());

        // Catalog contains at least one client-visible and one server-only flag.
        Assert.Contains(Hali.Application.FeatureFlags.FeatureFlags.MobileHomeConditionBadgeEnabled.Name, resolved);
        Assert.DoesNotContain(Hali.Application.FeatureFlags.FeatureFlags.WorkersPushDispatcherEnabled.Name, resolved);
    }

    [Fact]
    public void EvaluateClientVisible_ResolvesEachFlagAgainstContext()
    {
        IReadOnlyDictionary<string, bool> prodResolved = _service.EvaluateClientVisible(MakeContext(environment: "Production"));
        IReadOnlyDictionary<string, bool> devResolved = _service.EvaluateClientVisible(MakeContext(environment: "Development"));

        // MobileHomeConditionBadgeEnabled defaults to false in production and
        // flips on in Development via its targeting rule.
        string name = Hali.Application.FeatureFlags.FeatureFlags.MobileHomeConditionBadgeEnabled.Name;
        Assert.False(prodResolved[name]);
        Assert.True(devResolved[name]);
    }

    [Fact]
    public void Catalog_AllFlagsHaveNonEmptyNameDescriptionOwner()
    {
        // Policy guard: every flag in the catalog must be fully specified.
        foreach (BooleanFeatureFlag flag in Hali.Application.FeatureFlags.FeatureFlags.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(flag.Name), $"Flag must have a name");
            Assert.False(string.IsNullOrWhiteSpace(flag.Description), $"Flag {flag.Name} must have a description");
            Assert.False(string.IsNullOrWhiteSpace(flag.Owner), $"Flag {flag.Name} must have an owner");
        }
    }

    [Fact]
    public void Catalog_AllFlagNamesAreUnique()
    {
        HashSet<string> seen = new();
        foreach (BooleanFeatureFlag flag in Hali.Application.FeatureFlags.FeatureFlags.All)
        {
            Assert.True(seen.Add(flag.Name), $"Duplicate flag name: {flag.Name}");
        }
    }

    [Fact]
    public void Flag_NonPermanentRequiresExpectedRetirement()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new BooleanFeatureFlag(
            name: "test.bad.enabled",
            description: "test",
            owner: "@test",
            kind: FlagKind.DarkLaunch,
            visibility: FlagVisibility.ServerOnly,
            isPermanent: false,
            expectedRetirement: null,
            @default: true,
            targeting: new List<FlagRule>()));

        Assert.Contains("expected retirement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Flag_PermanentMustNotHaveExpectedRetirement()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new BooleanFeatureFlag(
            name: "test.bad.enabled",
            description: "test",
            owner: "@test",
            kind: FlagKind.KillSwitch,
            visibility: FlagVisibility.ServerOnly,
            isPermanent: true,
            expectedRetirement: new DateOnly(2030, 1, 1),
            @default: true,
            targeting: new List<FlagRule>()));

        Assert.Contains("expected retirement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_LifecycleMetadataIsConsistent()
    {
        // Every catalog entry satisfies the constructor invariants; this
        // test just enumerates to ensure the static initialisation didn't
        // throw (it would surface as a TypeInitializationException on
        // first access otherwise) and documents the expectation.
        foreach (BooleanFeatureFlag flag in Hali.Application.FeatureFlags.FeatureFlags.All)
        {
            if (flag.IsPermanent)
            {
                Assert.Null(flag.ExpectedRetirement);
            }
            else
            {
                Assert.NotNull(flag.ExpectedRetirement);
            }
        }
    }
}
