namespace Hali.Application.Signals;

public record NlpLocationDto(string? AreaName, string? RoadName, string? JunctionName, string? LandmarkName, string? FacilityName, string? LocationLabel, string? LocationPrecisionType, double LocationConfidence, string LocationSource);
