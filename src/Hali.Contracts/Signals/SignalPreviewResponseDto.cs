namespace Hali.Contracts.Signals;

public record SignalPreviewResponseDto(
    string Category,
    string SubcategorySlug,
    string? ConditionSlug,
    double ConditionConfidence,
    SignalLocationDto Location,
    string? TemporalType,
    string? NeutralSummary,
    bool ShouldSuggestJoin
);

public record SignalLocationDto(
    string? AreaName,
    string? RoadName,
    string? JunctionName,
    string? LandmarkName,
    string? FacilityName,
    string? LocationLabel,
    string? LocationPrecisionType,
    double LocationConfidence,
    string LocationSource
);
