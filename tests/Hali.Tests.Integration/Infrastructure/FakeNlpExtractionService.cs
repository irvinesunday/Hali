using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Returns a deterministic NLP result so preview/submit tests work without
/// calling the real Anthropic API.
/// </summary>
internal sealed class FakeNlpExtractionService : INlpExtractionService
{
    public Task<NlpExtractionResultDto?> ExtractAsync(
        NlpExtractionRequest request,
        CancellationToken ct = default)
    {
        var result = new NlpExtractionResultDto(
            Category:            "roads",
            Subcategory:         "potholes",
            ConditionLevel:      "pothole_severe",
            ConditionConfidence: 0.92,
            Location: new NlpLocationDto(
                AreaName:              "Test Area",
                RoadName:              "Test Road",
                JunctionName:          null,
                LandmarkName:          null,
                FacilityName:          null,
                LocationLabel:         "Test Road, Test Area",
                LocationPrecisionType: "road",
                LocationConfidence:    0.85,
                LocationSource:        "nlp"),
            TemporalHint: new NlpTemporalHintDto("temporary", 0.75),
            Summary:          "Severe pothole reported on Test Road.",
            ShouldSuggestJoin: false,
            ReasoningNotes:   new List<string> { "Integration test fixed response." });
        return Task.FromResult<NlpExtractionResultDto?>(result);
    }
}
