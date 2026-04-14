namespace Hali.Application.Signals;

/// <summary>
/// Single source of truth for whether an NLP-extracted location is too
/// weak to proceed without user correction.
///
/// Historically the composer's three-tier gate (required / confirm / accept)
/// lived only on the mobile client, which meant any second client would have
/// to re-derive the same thresholds. C11 centralizes the decision here so
/// that the <c>SignalPreviewResponseDto.RequiresLocationFallback</c> flag
/// is authoritative — the client still has a defensive client-side mirror
/// for offline / degraded network conditions, but the server decision wins
/// when it is available.
///
/// The rule is intentionally conservative: a high numeric confidence
/// paired with a blank label still fails the gate, because a label-less
/// extraction is not usable as a human-readable civic label even if the
/// model self-reports high confidence.
/// </summary>
public static class LocationFallbackPolicy
{
    /// <summary>
    /// Confidence strictly below this threshold is treated as low-confidence
    /// and triggers the fallback path. Mirrors the mobile
    /// <c>LOCATION_CONFIDENCE_WARN_THRESHOLD</c>.
    /// </summary>
    public const double LowConfidenceThreshold = 0.5;

    public static bool RequiresFallback(NlpLocationDto location)
    {
        if (location is null)
        {
            return true;
        }

        if (location.LocationConfidence < LowConfidenceThreshold)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(location.LocationLabel))
        {
            return true;
        }

        return false;
    }
}
