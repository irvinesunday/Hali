using System;

namespace Hali.Contracts.Notifications;

/// <summary>
/// Minimal ward/locality summary used by the canonical ward list endpoint
/// (GET /v1/localities/wards). The client caches this list and performs
/// its own fast, client-side search/filter over the results.
/// </summary>
public class LocalitySummaryDto
{
    public Guid LocalityId { get; set; }

    public string WardName { get; set; } = string.Empty;

    public string? CityName { get; set; }
}
