namespace Hali.Application.Signals;

public record NlpExtractionRequest(string FreeText, double? UserLatitude, double? UserLongitude, string? SelectedWard, string? Locale, string? KnownCity, string? CountryCode, string CurrentTimeUtc, string TaxonomyBlock);
