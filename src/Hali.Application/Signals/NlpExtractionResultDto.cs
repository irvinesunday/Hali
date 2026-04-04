using System.Collections.Generic;

namespace Hali.Application.Signals;

public record NlpExtractionResultDto(string Category, string Subcategory, string? ConditionLevel, double ConditionConfidence, NlpLocationDto Location, NlpTemporalHintDto? TemporalHint, string? Summary, bool ShouldSuggestJoin, List<string>? ReasoningNotes);
