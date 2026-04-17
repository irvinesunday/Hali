using System.Collections.Generic;

namespace Hali.Application.FeatureFlags;

// Typed flag catalog. Every flag in Hali lives here. Adding a flag is a
// PR that (a) creates the BooleanFeatureFlag definition below, (b) wires
// the consumer call site, and (c) states the rollout / rollback /
// retirement plan in the PR description per
// docs/arch/FEATURE_FLIGHTING_MODEL.md §7.
//
// Retire a flag by deleting its entry and every consumer reference.
// Never rename; add a new flag, dark-launch it, retire the old one.

public static class FeatureFlags
{
    // ── Server-only kill switch — deliberate example so the registry is
    // not empty and the wiring is exercised end-to-end. Concrete
    // application is to gate push-dispatcher pickup if Expo Push is
    // failing and we want to stop retries without a code change.
    public static readonly BooleanFeatureFlag WorkersPushDispatcherEnabled = new(
        Name: "workers.push_dispatcher.enabled",
        Description: "Gates the Expo push dispatcher worker pickup. Flip off to stop new sends; retries drain normally.",
        Owner: "@irvinesunday",
        Kind: FlagKind.KillSwitch,
        Visibility: FlagVisibility.ServerOnly,
        Default: true,
        Targeting: new List<FlagRule>());

    // ── Client-visible dark launch — an example the mobile client will
    // consume once #214 lands. Default off in production.
    public static readonly BooleanFeatureFlag MobileHomeConditionBadgeEnabled = new(
        Name: "mobile.home.condition_badge.enabled",
        Description: "Shows the condition badge in the cluster header on the citizen home feed.",
        Owner: "@irvinesunday",
        Kind: FlagKind.DarkLaunch,
        Visibility: FlagVisibility.ClientVisible,
        Default: false,
        Targeting: new List<FlagRule>
        {
            new() { Environment = "Development", Value = true },
        });

    /// <summary>
    /// Complete catalog. Kept in the same declaration order as the fields
    /// above; enumeration order is stable for consumers.
    /// </summary>
    public static readonly IReadOnlyList<BooleanFeatureFlag> All = new[]
    {
        WorkersPushDispatcherEnabled,
        MobileHomeConditionBadgeEnabled,
    };
}
