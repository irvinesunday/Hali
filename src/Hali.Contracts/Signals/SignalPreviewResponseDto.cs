namespace Hali.Contracts.Signals;

public record SignalPreviewResponseDto(string Category, string SubcategorySlug, string? ConditionSlug, double ConditionConfidence, SignalLocationDto Location, string? TemporalType, string? NeutralSummary, bool ShouldSuggestJoin);
