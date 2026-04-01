namespace Hali.Contracts.Signals;

public record SignalLocationDto(string? AreaName, string? RoadName, string? JunctionName, string? LandmarkName, string? FacilityName, string? LocationLabel, string? LocationPrecisionType, double LocationConfidence, string LocationSource);
