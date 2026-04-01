namespace Hali.Application.Signals;

public record NlpExtractionRequest(
    string FreeText,
    double? UserLatitude,
    double? UserLongitude,
    string? SelectedWard,
    string? Locale,
    string? KnownCity,
    string? CountryCode,
    string CurrentTimeUtc,
    /// <summary>
    /// Canonical taxonomy lines injected into the prompt.
    /// Format: "category: subcategory1, subcategory2"
    /// </summary>
    string TaxonomyBlock
);
