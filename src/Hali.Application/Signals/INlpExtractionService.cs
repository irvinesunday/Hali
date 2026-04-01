namespace Hali.Application.Signals;

public interface INlpExtractionService
{
    /// <summary>
    /// Calls Anthropic Claude API and returns structured extraction result.
    /// Returns null if the response is malformed or fails backend validation.
    /// </summary>
    Task<NlpExtractionResultDto?> ExtractAsync(NlpExtractionRequest request, CancellationToken ct = default);
}
