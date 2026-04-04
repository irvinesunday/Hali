namespace Hali.Contracts.Signals;

public record SignalSubmitRequestDto(string IdempotencyKey, string DeviceHash, string FreeText, string Category, string SubcategorySlug, string? ConditionSlug, double ConditionConfidence, double? Latitude, double? Longitude, string? LocationLabel, string? LocationPrecisionType, double LocationConfidence, string LocationSource, string? TemporalType, string? NeutralSummary, string? SourceLanguage, string? SpatialCellId);
