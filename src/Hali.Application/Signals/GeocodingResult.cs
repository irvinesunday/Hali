namespace Hali.Application.Signals;

public record GeocodingResult(
    string? DisplayName,
    string? Road,
    string? Suburb,
    string? City,
    string? Country
);
