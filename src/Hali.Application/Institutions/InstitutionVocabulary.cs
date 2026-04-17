using System.Collections.Generic;

namespace Hali.Application.Institutions;

/// <summary>
/// Canonical bounded vocabularies for the institution operational surface.
/// These values are the wire contract — see
/// <c>docs/arch/hali_institution_backend_contract_implications.md §4</c>.
/// Keep this file in sync with the corresponding OpenAPI enum arrays and
/// client-side taxonomy files.
/// </summary>
public static class InstitutionVocabulary
{
    // Response statuses attached to live_update official posts. Keep the
    // HashSet lowercase and compare against normalised input only.
    public static readonly IReadOnlySet<string> ResponseStatuses = new HashSet<string>
    {
        "acknowledged",
        "teams_dispatched",
        "teams_on_site",
        "work_ongoing",
        "restoration_in_progress",
        "service_restored",
    };

    // Severity levels attached to scheduled_disruption official posts.
    public static readonly IReadOnlySet<string> Severities = new HashSet<string>
    {
        "minor",
        "moderate",
        "major",
    };

    // Signal list filter states consumed by the Live Signals page.
    // Iteration order is deterministic (List, not HashSet) so the validation
    // error message that lists the accepted values is stable across runs.
    public static readonly IReadOnlyList<string> SignalFilterStates = new List<string>
    {
        "active",
        "growing",
        "needs_attention",
        "restoration",
    };

    // Area conditions surfaced on the Overview and Areas pages.
    public const string ConditionActive = "active";
    public const string ConditionElevated = "elevated";
    public const string ConditionCalm = "calm";

    // Signal trends used on the list + detail views. Derived from recent
    // report momentum; not currently persisted (computed on read).
    public const string TrendStable = "stable";
    public const string TrendGrowing = "growing";
    public const string TrendSlowing = "slowing";
    public const string TrendPossibleRestoration = "possible_restoration";

    // Activity feed item types — the subset currently emitted by the
    // institution activity endpoint. The OpenAPI enum also lists
    // <c>stabilising</c> as a reserved wire value; it has no emission site
    // today and will be wired once the cluster-lifecycle telemetry
    // distinguishes "activity slowing without full restoration" from the
    // generic <c>growing</c> state. Adding a new active emission requires a
    // matching CASE branch in InstitutionReadRepository.GetActivityAsync and
    // an OpenAPI update.
    public const string ActivityNewSignal = "new_signal";
    public const string ActivityGrowing = "growing";
    public const string ActivityUpdatePosted = "update_posted";
    public const string ActivityRestoration = "restoration";
    public const string ActivityRestored = "restored";
}
