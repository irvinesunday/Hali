namespace Hali.Contracts.Institutions;

/// <summary>
/// Request body for <c>POST /v1/institution/clusters/{clusterId}/acknowledge</c>.
/// The institution explicitly acknowledges a cluster in its jurisdiction;
/// the server emits an <c>institution.action.recorded</c> outbox event so
/// the action is auditable without inferring intent from a passive view
/// (passive-view tracking is deferred — see the Phase 4 integration audit
/// P1 backlog).
/// </summary>
/// <param name="IdempotencyKey">Required per the mutation-endpoint idempotency
/// rule (<c>docs/arch/02_api_contracts.md</c> §Idempotency). Re-submitting
/// the same key returns the existing record without emitting a duplicate
/// outbox event.</param>
/// <param name="Note">Optional free-text note attached to the acknowledgement
/// (e.g. "triaged, works dispatched"). Bounded to 500 chars. Persisted only
/// in the outbox payload.</param>
public sealed record InstitutionAcknowledgeRequestDto(
    string IdempotencyKey,
    string? Note);

/// <summary>
/// Response for a successful acknowledgement (or an idempotent replay).
/// Shape matches <c>02_openapi.yaml</c> §acknowledge 202 response.
/// </summary>
public sealed record InstitutionAcknowledgeResponseDto(
    System.Guid AcknowledgementId,
    System.Guid ClusterId,
    System.DateTime RecordedAt);
