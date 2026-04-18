using System;
using System.Collections.Generic;

namespace Hali.Contracts.Institutions;

/// <summary>
/// Response of <c>GET /v1/institution/restoration</c> — the queue of
/// clusters in <c>possible_restoration</c> inside the caller's
/// jurisdiction. Populated by the citizen-vote and institution-claim
/// restoration paths; the queue drains when a cluster flips to
/// <c>resolved</c> (ratio met) or <c>active</c> (still-affected votes
/// overrule). Vote counts + ratio come from the same
/// <c>RestorationCountSnapshot</c> that the server uses to drive the
/// lifecycle transition, so the dashboard shows exactly the evidence
/// the engine evaluated.
/// </summary>
public sealed record InstitutionRestorationQueueResponseDto(
    IReadOnlyList<InstitutionRestorationQueueItemDto> Items);

public sealed record InstitutionRestorationQueueItemDto(
    Guid ClusterId,
    string Title,
    string Category,
    Guid? LocalityId,
    string? LocalityName,
    DateTime PossibleRestorationAt,
    int RestorationYes,
    int StillAffected,
    int TotalRestorationResponses,
    double? RestorationRatio);
