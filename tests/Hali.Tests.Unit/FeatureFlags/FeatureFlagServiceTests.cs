using System;
using System.Collections.Generic;
using Hali.Application.FeatureFlags;
using Xunit;

namespace Hali.Tests.Unit.FeatureFlags;

public class FeatureFlagServiceTests
{
    private readonly FeatureFlagService _service = new();

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
        BooleanFeatureFlag flag = new(
            Name: "test.flag.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.DarkLaunch,
            Visibility: FlagVisibility.ServerOnly,
            Default: true,
            Targeting: new List<FlagRule>());

        Assert.True(_service.Evaluate(flag, MakeContext()));
    }

    [Fact]
    public void Evaluate_EnvironmentRule_Matches()
    {
        BooleanFeatureFlag flag = new(
            Name: "test.env.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.DarkLaunch,
            Visibility: FlagVisibility.ServerOnly,
            Default: false,
            Targeting: new List<FlagRule>
            {
                new() { Environment = "Development", Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(environment: "Development")));
        Assert.False(_service.Evaluate(flag, MakeContext(environment: "Production")));
    }

    [Fact]
    public void Evaluate_InstitutionIdRule_Matches()
    {
        Guid targetInstitution = Guid.NewGuid();
        BooleanFeatureFlag flag = new(
            Name: "test.institution.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.Pilot,
            Visibility: FlagVisibility.ClientVisible,
            Default: false,
            Targeting: new List<FlagRule>
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
        BooleanFeatureFlag flag = new(
            Name: "test.locality.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.Pilot,
            Visibility: FlagVisibility.ClientVisible,
            Default: false,
            Targeting: new List<FlagRule>
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
        BooleanFeatureFlag flag = new(
            Name: "test.actor.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.InternalOnly,
            Visibility: FlagVisibility.ClientVisible,
            Default: false,
            Targeting: new List<FlagRule>
            {
                new() { ActorTypes = new HashSet<string> { "admin" }, Value = true },
            });

        Assert.True(_service.Evaluate(flag, MakeContext(actorType: "admin")));
        Assert.False(_service.Evaluate(flag, MakeContext(actorType: "citizen")));
    }

    [Fact]
    public void Evaluate_MultipleRules_FirstMatchWins()
    {
        BooleanFeatureFlag flag = new(
            Name: "test.first_match.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.Pilot,
            Visibility: FlagVisibility.ServerOnly,
            Default: false,
            Targeting: new List<FlagRule>
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
        BooleanFeatureFlag flag = new(
            Name: "test.combined.enabled",
            Description: "test",
            Owner: "@test",
            Kind: FlagKind.Pilot,
            Visibility: FlagVisibility.ClientVisible,
            Default: false,
            Targeting: new List<FlagRule>
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
}
