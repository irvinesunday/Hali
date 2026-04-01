namespace Hali.Contracts.Signals;

public record SignalPreviewRequestDto(
    string FreeText,
    double? UserLatitude,
    double? UserLongitude,
    string? SelectedWard,
    string? Locale,
    string? KnownCity,
    string? CountryCode
);
