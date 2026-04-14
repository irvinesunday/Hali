namespace Hali.Contracts.Signals;

/// <summary>
/// Response from <c>POST /v1/signals/preview</c>.
///
/// <c>RequiresLocationFallback</c> is the server-authoritative signal that
/// the NLP-extracted location is too weak to submit as-is and the composer
/// should route the user into the place-search / current-location fallback
/// UX. See <c>LocationFallbackPolicy</c> for the exact rule. The mobile
/// client maintains a defensive client-side mirror of the same rule for
/// offline degradation, but when the server flag is present it wins.
/// </summary>
public record SignalPreviewResponseDto(
    string Category,
    string SubcategorySlug,
    string? ConditionSlug,
    double ConditionConfidence,
    SignalLocationDto Location,
    string? TemporalType,
    string? NeutralSummary,
    bool ShouldSuggestJoin,
    bool RequiresLocationFallback);
