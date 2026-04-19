using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.Signals;

public record SignalPreviewRequestDto(
    [property: Required]
    [property: MaxLength(150)]
    string FreeText,
    double? UserLatitude,
    double? UserLongitude,
    string? SelectedWard,
    string? Locale,
    string? KnownCity,
    string? CountryCode);
