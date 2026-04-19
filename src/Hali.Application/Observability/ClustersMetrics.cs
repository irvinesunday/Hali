using System;
using System.Diagnostics.Metrics;

namespace Hali.Application.Observability;

/// <summary>
/// Hosts the <c>Hali.Clusters</c> <see cref="Meter"/> and the instruments
/// covering participation activity and cluster lifecycle transitions (R3.c,
/// issue #168). These two concerns share a meter because participation
/// directly drives lifecycle: an <c>affected</c> / <c>restoration_response</c>
/// submission is what trips the state machine from <c>active</c> to
/// <c>possible_restoration</c>, and operators correlate the two signals on the
/// same dashboard.
///
/// Before this meter, participation counts and lifecycle transitions were
/// only visible through the structured log stream (<c>ClusterActivated</c>,
/// <c>ClusterPossibleRestoration</c>, <c>ClusterResolvedByDecay</c>) and
/// through the <c>outbox_events</c> table — neither is a queryable
/// time-series surface for pilot alerting.
///
/// <list type="bullet">
///   <item><description><c>participation_actions_total</c> — wire-visible
///     counter over the three participation endpoints
///     (<c>/participation</c>, <c>/context</c>,
///     <c>/restoration-response</c>), tagged by the action attempted and the
///     outcome. Emitted once per non-cancelled call from
///     <c>ClustersController</c>.</description></item>
///   <item><description><c>cluster_lifecycle_transitions_total</c> — counter
///     over the Phase 1 lifecycle state machine
///     (<c>unconfirmed → active → possible_restoration → resolved</c>),
///     tagged with <c>from_state</c> / <c>to_state</c>. Emitted at each of
///     the three code paths that mutate <c>SignalCluster.State</c>: CIVIS
///     activation, CIVIS decay, and the restoration-ratio evaluation in
///     <c>ParticipationService</c>.</description></item>
/// </list>
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>Program.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour
/// regresses.
///
/// The class lives in <c>Hali.Application.Observability</c> (same home as
/// <see cref="SignalsMetrics"/>) because the instruments are emitted from
/// both the API tier (<c>ClustersController</c>) and the Application tier
/// (<c>CivisEvaluationService</c>, <c>ParticipationService</c>). The
/// Application project is the lowest common reference point.
///
/// Tag values are bounded by static catalogs — no cluster id, locality id,
/// locality name, ward name, account id, device id, correlation id,
/// category, subcategory, restoration vote count, or any CIVIS internal
/// (civis_score, wrab, sds, macf, raw_confirmation_count) is ever attached.
/// At steady state the two counters produce at most
/// 6 × 3 + 3 × 3 = <b>27</b> time series (and in practice the lifecycle
/// counter only populates three <c>from_state/to_state</c> pairs:
/// <c>unconfirmed→active</c>, <c>active→possible_restoration</c>,
/// <c>possible_restoration→resolved</c>).
///
/// <para>
/// <b>Overlap with <see cref="SignalsMetrics.SignalJoinOutcomeTotal"/>.</b>
/// The signals meter already counts <c>activated_cluster</c> as one of the
/// three <c>signal_join_outcome_total</c> outcomes (emitted from
/// <c>CivisEvaluationService</c> on the same line the activation log fires).
/// That counter is signal-scoped — "what did the submitted signal do?". The
/// lifecycle counter here is cluster-scoped — "which transition did the
/// cluster just make?". They are deliberately additive: for the
/// <c>unconfirmed→active</c> transition both increment by one, and the two
/// counts will agree. The lifecycle counter is the canonical source for the
/// other two transitions (<c>active→possible_restoration</c>,
/// <c>possible_restoration→resolved</c>), which the signals meter does not
/// cover.
/// </para>
/// </summary>
public sealed class ClustersMetrics : IDisposable
{
    /// <summary>
    /// Name of the clusters <see cref="Meter"/>. Mirrored by
    /// <c>AddMeter(ClustersMetrics.MeterName)</c> on the OpenTelemetry meter
    /// provider so instruments export through the existing OTLP transport.
    /// </summary>
    public const string MeterName = "Hali.Clusters";

    /// <summary>Counter name — participation actions attempted against a cluster.</summary>
    public const string ParticipationActionsTotalName = "participation_actions_total";

    /// <summary>Counter name — cluster lifecycle state transitions.</summary>
    public const string ClusterLifecycleTransitionsTotalName = "cluster_lifecycle_transitions_total";

    // ── Tag keys ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tag key on <see cref="ParticipationActionsTotal"/> identifying the
    /// action the caller attempted. Values are drawn from
    /// <see cref="ActionTypeAffected"/>, <see cref="ActionTypeObserving"/>,
    /// <see cref="ActionTypeNoLongerAffected"/>,
    /// <see cref="ActionTypeContext"/>,
    /// <see cref="ActionTypeRestorationResponse"/>, and
    /// <see cref="ActionTypeUnknown"/> (for <c>/participation</c> calls that
    /// fail validation before the action type can be parsed).
    /// </summary>
    public const string TagActionType = "action_type";

    /// <summary>
    /// Tag key on <see cref="ParticipationActionsTotal"/> identifying the
    /// wire-visible outcome. Values: <see cref="OutcomeAccepted"/> (2xx),
    /// <see cref="OutcomeRejectedValidation"/> (4xx — missing fields,
    /// invalid type, unknown device, context window expired, restoration
    /// requires affected), or <see cref="OutcomeDependencyError"/> (5xx —
    /// unmapped application exceptions, server-side timeouts).
    /// </summary>
    public const string TagOutcome = "outcome";

    /// <summary>
    /// Tag key on <see cref="ClusterLifecycleTransitionsTotal"/> identifying
    /// the prior <c>SignalState</c>. Values are drawn from
    /// <see cref="StateUnconfirmed"/>, <see cref="StateActive"/>,
    /// <see cref="StatePossibleRestoration"/>.
    /// </summary>
    public const string TagFromState = "from_state";

    /// <summary>
    /// Tag key on <see cref="ClusterLifecycleTransitionsTotal"/> identifying
    /// the new <c>SignalState</c>. Values are drawn from
    /// <see cref="StateActive"/>, <see cref="StatePossibleRestoration"/>,
    /// <see cref="StateResolved"/>.
    /// </summary>
    public const string TagToState = "to_state";

    // ── Action-type tag values ──────────────────────────────────────────────
    // The three participation endpoints map to action_type as follows:
    //   POST /v1/clusters/{id}/participation         → affected | observing | no_longer_affected | unknown
    //   POST /v1/clusters/{id}/context               → context
    //   POST /v1/clusters/{id}/restoration-response  → restoration_response
    // "unknown" is only used on the /participation endpoint when the request
    // fails validation (missing/invalid `type`) before the type is parsed.
    // Keeping that bucket makes validation-error counts truthful even when
    // the payload is unusable.

    /// <summary>Action type: citizen marked themselves affected.</summary>
    public const string ActionTypeAffected = "affected";

    /// <summary>Action type: citizen marked themselves observing.</summary>
    public const string ActionTypeObserving = "observing";

    /// <summary>Action type: citizen cleared a prior affected participation.</summary>
    public const string ActionTypeNoLongerAffected = "no_longer_affected";

    /// <summary>Action type: citizen added free-text context to an affected participation.</summary>
    public const string ActionTypeContext = "context";

    /// <summary>Action type: citizen replied to a restoration prompt (restored / still_affected / not_sure).</summary>
    public const string ActionTypeRestorationResponse = "restoration_response";

    /// <summary>
    /// Action type: a <c>/participation</c> request that failed validation
    /// before the <c>type</c> field could be parsed (missing/unknown value).
    /// Only ever emitted with <see cref="OutcomeRejectedValidation"/>.
    /// </summary>
    public const string ActionTypeUnknown = "unknown";

    // ── Outcome tag values ──────────────────────────────────────────────────
    // The issue (#168) defined two buckets — accepted / rejected_validation —
    // which cover the two dominant dispositions: a successful 2xx and a
    // user-visible 4xx. A third bucket (dependency_error) is added to stay
    // consistent with the SignalsMetrics / ApiMetrics outcome taxonomy and
    // to keep 5xx failures from vanishing into the accepted bucket on
    // error paths the controller does not explicitly classify. This matches
    // the three-way taxonomy already established by R3.b (#167).

    /// <summary>Outcome: request succeeded (controller returned 2xx).</summary>
    public const string OutcomeAccepted = "accepted";

    /// <summary>
    /// Outcome: request rejected due to user input or pre-conditions —
    /// covers ValidationException (missing / invalid fields, unknown device),
    /// ConflictException (context requires affected, context window expired,
    /// restoration requires affected), and RateLimitException.
    /// </summary>
    public const string OutcomeRejectedValidation = "rejected_validation";

    /// <summary>
    /// Outcome: request failed due to a dependency or server error —
    /// DependencyException, unmapped AppException subclasses, server-side
    /// timeouts (<c>OperationCanceledException</c> with an unsignaled caller
    /// token), and any other exception translated to 5xx by
    /// <c>ExceptionHandlingMiddleware</c>. True caller cancellation
    /// (<c>ct.IsCancellationRequested</c> at catch time) is excluded from
    /// the taxonomy entirely — see <c>ClustersController</c> for the guard.
    /// </summary>
    public const string OutcomeDependencyError = "dependency_error";

    // ── State tag values ────────────────────────────────────────────────────
    // Canonical snake_case values used by the cluster_lifecycle_transitions
    // counter. These are deliberately NOT derived from SignalState.ToString()
    // because `SignalState.PossibleRestoration.ToString().ToLowerInvariant()`
    // yields "possiblerestoration" (no underscore), which violates
    // `docs/arch/CODING_STANDARDS.md` §Enum serialization rules.
    // The `cluster_state_changed` outbox payload emits the same canonical
    // strings — all five authoritative transition paths use these constants
    // or equivalent string literals, so dashboards, alerts, and downstream
    // outbox consumers observe the same value for the same transition
    // (see issue #178 for the fix that removed the prior
    // `"possiblerestoration"` divergence in `ApplyDecayAsync`).
    // Only the four states that participate in lifecycle transitions are
    // listed here — Expired and Suppressed are not produced by any of the
    // instrumented code paths and are omitted from the tag catalog to keep
    // cardinality honest.

    /// <summary>State: cluster created but not yet confirmed.</summary>
    public const string StateUnconfirmed = "unconfirmed";

    /// <summary>State: cluster active (MACF + device diversity met).</summary>
    public const string StateActive = "active";

    /// <summary>State: restoration being evaluated (decay or restoration vote).</summary>
    public const string StatePossibleRestoration = "possible_restoration";

    /// <summary>State: cluster resolved.</summary>
    public const string StateResolved = "resolved";

    private readonly Meter _meter;

    /// <summary>
    /// <c>participation_actions_total</c> — incremented exactly once per
    /// non-cancelled call to any of the three participation endpoints on
    /// <c>ClustersController</c>. The controller is the truthful point of
    /// observation because it is the only layer that knows both the
    /// attempted action (from the route and, for <c>/participation</c>,
    /// the parsed <c>type</c>) and the wire-visible outcome (from its
    /// try/catch taxonomy).
    ///
    /// Tags (bounded — 6 × 3 = 18 combinations max):
    /// <list type="bullet">
    ///   <item><description><see cref="TagActionType"/> —
    ///     <c>affected | observing | no_longer_affected | context |
    ///     restoration_response | unknown</c>.</description></item>
    ///   <item><description><see cref="TagOutcome"/> —
    ///     <c>accepted | rejected_validation | dependency_error</c>.</description></item>
    /// </list>
    /// No cluster id, account id, device id, device hash, correlation id,
    /// idempotency key, restoration response value, or context text is ever
    /// attached.
    /// </summary>
    public Counter<long> ParticipationActionsTotal { get; }

    /// <summary>
    /// <c>cluster_lifecycle_transitions_total</c> — incremented exactly once
    /// per persisted cluster state transition, colocated with the
    /// <c>ApplyClusterTransitionAsync</c> call that atomically commits the
    /// state change, CIVIS decision, and outbox event.
    ///
    /// Tags (bounded — 4 actual pairs at steady state):
    /// <list type="bullet">
    ///   <item><description><see cref="TagFromState"/> —
    ///     <c>unconfirmed | active | possible_restoration</c>.</description></item>
    ///   <item><description><see cref="TagToState"/> —
    ///     <c>active | possible_restoration | resolved</c>.</description></item>
    /// </list>
    /// Emitted from five places (one per authoritative transition path):
    /// <list type="bullet">
    ///   <item><description><c>CivisEvaluationService.EvaluateClusterAsync</c>
    ///     — <c>unconfirmed → active</c> (MACF + device diversity met).</description></item>
    ///   <item><description><c>CivisEvaluationService.ApplyDecayAsync</c> —
    ///     <c>active → possible_restoration</c> (decay below threshold).</description></item>
    ///   <item><description><c>ParticipationService.EvaluateRestorationAsync</c>
    ///     — <c>active → possible_restoration</c> (restoration ratio met
    ///     from citizen votes).</description></item>
    ///   <item><description><c>OfficialPostsService</c>
    ///     — <c>active → possible_restoration</c> (institution restoration
    ///     claim).</description></item>
    ///   <item><description><c>RestorationEvaluationService.EvaluateAsync</c>
    ///     — <c>possible_restoration → active</c> (still-affected votes
    ///     dominate) and <c>possible_restoration → resolved</c> (citizen
    ///     restoration threshold met).</description></item>
    /// </list>
    /// No cluster id, locality id, category, reason code, CIVIS metric, or
    /// restoration vote count is ever attached.
    /// </summary>
    public Counter<long> ClusterLifecycleTransitionsTotal { get; }

    public ClustersMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        ParticipationActionsTotal = _meter.CreateCounter<long>(
            name: ParticipationActionsTotalName,
            unit: "{action}",
            description: "Number of participation-endpoint calls, tagged by action type and wire-visible outcome.");

        ClusterLifecycleTransitionsTotal = _meter.CreateCounter<long>(
            name: ClusterLifecycleTransitionsTotalName,
            unit: "{transition}",
            description: "Number of persisted cluster state transitions, tagged by from_state and to_state.");
    }

    public void Dispose() => _meter.Dispose();
}
