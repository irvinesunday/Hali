namespace Hali.Application.Signals;

/// <summary>
/// Internal DTO that maps 1:1 from NLP JSON output keys.
/// Field names match the NLP JSON schema, not API or DB column names.
/// Mapping per nlp_extraction_prompt.md Field Name Mapping table.
/// </summary>
public record NlpExtractionResultDto(
    string Category,
    string Subcategory,
    string? ConditionLevel,
    double ConditionConfidence,
    NlpLocationDto Location,
    NlpTemporalHintDto? TemporalHint,
    string? Summary,
    bool ShouldSuggestJoin,
    List<string>? ReasoningNotes
);

public record NlpLocationDto(
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

public record NlpTemporalHintDto(
    string Type,
    double Confidence
);
