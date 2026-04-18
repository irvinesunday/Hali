using System.Text.RegularExpressions;

namespace Hali.Application.Observability;

/// <summary>
/// Stable event name constants for structured logging.
/// All names are dot-delimited, lowercase, metrics-friendly.
/// </summary>
public static class ObservabilityEvents
{
    /// <summary>
    /// Maximum length of a user-supplied value rendered into a structured log field.
    /// Keeps log lines bounded.
    /// </summary>
    private const int MaxLogFieldLength = 64;

    private static readonly Regex SafeChars = new(@"[^a-zA-Z0-9_\-.]", RegexOptions.Compiled);

    /// <summary>
    /// Strips control characters and non-alphanumeric chars (except underscore, hyphen, dot),
    /// truncates to a bounded length. Safe for structured log fields derived from user input.
    /// </summary>
    public static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        string cleaned = SafeChars.Replace(value, string.Empty);
        if (cleaned.Length > MaxLogFieldLength)
            cleaned = cleaned[..MaxLogFieldLength];
        return cleaned;
    }

    // ── Home feed ────────────────────────────────────────────────────────────
    public const string HomeRequestStarted = "home.request.started";
    public const string HomeRequestCompleted = "home.request.completed";
    public const string HomeRequestFailed = "home.request.failed";
    public const string HomeSectionBuilt = "home.section.built";
    public const string HomeCacheChecked = "home.cache.checked";
    public const string HomeCacheHit = "home.cache.hit";
    public const string HomeCacheMiss = "home.cache.miss";
    public const string HomeLocalityScopeExplicit = "home.locality.explicit";
    public const string HomeLocalityScopeFallback = "home.locality.fallback";
    public const string HomeLocalityScopeGuestEmpty = "home.locality.guest_empty";

    // ── Signal submit ────────────────────────────────────────────────────────
    public const string SignalSubmitStarted = "signal.submit.started";
    public const string SignalSubmitCompleted = "signal.submit.completed";
    public const string SignalSubmitFailed = "signal.submit.failed";
    public const string SignalLocalityResolved = "signal.locality.resolved";
    public const string SignalLocalityFailed = "signal.locality.failed";
    public const string SignalSpatialDerived = "signal.spatial.derived";
    public const string SignalSpatialFailed = "signal.spatial.failed";

    // ── Signal routing ───────────────────────────────────────────────────────
    public const string SignalRouted = "signal.routed";
    public const string SignalClusterCreated = "signal.cluster.created";
    public const string SignalClusterJoined = "signal.cluster.joined";

    // ── Cluster lifecycle ──────────────────────────────────────────────────
    // These constants double as canonical outbox `event_type` values. Every
    // cluster state transition writes the same string to the outbox that the
    // structured log emits; see Phase 4 outbox taxonomy in
    // `docs/arch/02_api_contracts.md` §Outbox event envelope.
    public const string SignalSubmitted = "signal.submitted";
    public const string ClusterCreated = "cluster.created";
    public const string ClusterActivated = "cluster.activated";
    public const string ClusterPossibleRestoration = "cluster.possible_restoration";
    public const string ClusterRestorationConfirmed = "cluster.restoration_confirmed";
    public const string ClusterRevertedToActive = "cluster.reverted_to_active";
    public const string ClusterResolvedByDecay = "cluster.resolved_by_decay";

    // ── Institution actions ────────────────────────────────────────────────
    public const string InstitutionActionRecorded = "institution.action.recorded";

    // ── Outbox schema versions ─────────────────────────────────────────────
    // Each canonical event starts at schema_version "1.0" and increments on
    // breaking payload changes. Kept centralised so publishers and consumers
    // cite the same constant.
    public const string SchemaVersionV1 = "1.0";
}
