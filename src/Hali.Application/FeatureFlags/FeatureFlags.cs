using System;
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
        name: "workers.push_dispatcher.enabled",
        description: "Gates the Expo push dispatcher worker pickup. Flip off to stop new sends; retries drain normally.",
        owner: "@irvinesunday",
        kind: FlagKind.KillSwitch,
        visibility: FlagVisibility.ServerOnly,
        isPermanent: true,
        expectedRetirement: null,
        @default: true,
        targeting: new List<FlagRule>());

    // ── Client-visible dark launch — consumed by the citizen mobile
    // cluster header once the condition badge feature is wired.
    // Defaults off; flipped on in Development via targeting.
    public static readonly BooleanFeatureFlag MobileHomeConditionBadgeEnabled = new(
        name: "mobile.home.condition_badge.enabled",
        description: "Shows the condition badge in the cluster header on the citizen home feed.",
        owner: "@irvinesunday",
        kind: FlagKind.DarkLaunch,
        visibility: FlagVisibility.ClientVisible,
        isPermanent: false,
        expectedRetirement: new DateOnly(2026, 9, 30),
        @default: false,
        targeting: new List<FlagRule>
        {
            new() { Environment = "Development", Value = true },
        });

    // ── Phase 2 institution dashboard master gate. When off, the shell
    // renders a locked-out state regardless of the user's grants — used
    // to roll back the surface without a redeploy. Defaults off; flipped
    // on in Development via targeting and flipped on per institution in
    // Staging/Production as the rollout progresses.
    public static readonly BooleanFeatureFlag InstitutionWebEnabled = new(
        name: "institution_web.enabled",
        description: "Master gate for the institution operations dashboard shell.",
        owner: "@irvinesunday",
        kind: FlagKind.Pilot,
        visibility: FlagVisibility.ClientVisible,
        isPermanent: false,
        expectedRetirement: new DateOnly(2026, 12, 31),
        @default: false,
        targeting: new List<FlagRule>
        {
            new() { Environment = "Development", Value = true },
        });

    // ── Gates the "Post an update" composer on the cluster detail
    // screen. Separate from the master gate so a failing mutation can
    // be disabled without losing read-only dashboard visibility.
    public static readonly BooleanFeatureFlag InstitutionWebPostUpdateEnabled = new(
        name: "institution_web.post_update.enabled",
        description: "Enables the official-update composer on cluster detail (institution dashboard).",
        owner: "@irvinesunday",
        kind: FlagKind.KillSwitch,
        visibility: FlagVisibility.ClientVisible,
        isPermanent: true,
        expectedRetirement: null,
        @default: true,
        targeting: new List<FlagRule>());

    // ── Gates the restoration-claim CTA on the cluster detail screen.
    // Separate from the post-update flag because restoration triggers
    // a backend state transition; a failure mode in the transition
    // pipeline can be isolated without disabling other update kinds.
    public static readonly BooleanFeatureFlag InstitutionWebRestorationClaimEnabled = new(
        name: "institution_web.restoration_claim.enabled",
        description: "Enables the institution restoration-claim action on cluster detail.",
        owner: "@irvinesunday",
        kind: FlagKind.KillSwitch,
        visibility: FlagVisibility.ClientVisible,
        isPermanent: true,
        expectedRetirement: null,
        @default: true,
        targeting: new List<FlagRule>());

    /// <summary>
    /// Complete catalog. Kept in the same declaration order as the fields
    /// above; enumeration order is stable for consumers.
    /// </summary>
    public static readonly IReadOnlyList<BooleanFeatureFlag> All = new[]
    {
        WorkersPushDispatcherEnabled,
        MobileHomeConditionBadgeEnabled,
        InstitutionWebEnabled,
        InstitutionWebPostUpdateEnabled,
        InstitutionWebRestorationClaimEnabled,
    };
}
