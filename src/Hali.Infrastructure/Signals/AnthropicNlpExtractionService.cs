using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hali.Application.Signals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hali.Infrastructure.Signals;

public class AnthropicNlpExtractionService : INlpExtractionService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<AnthropicNlpExtractionService> _logger;

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure"
    };

    private static readonly HashSet<string> AllowedTemporalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "temporary", "continuous", "recurring", "scheduled", "episodic_unknown"
    };

    public AnthropicNlpExtractionService(
        HttpClient http,
        IConfiguration config,
        ILogger<AnthropicNlpExtractionService> logger)
    {
        _http = http;
        _apiKey = config["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic:ApiKey is required");
        _model = config["Anthropic:Model"] ?? "claude-sonnet-4-6";
        _logger = logger;
    }

    public async Task<NlpExtractionResultDto?> ExtractAsync(NlpExtractionRequest request, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(request);

        var body = new
        {
            model = _model,
            max_tokens = 1024,
            temperature = 0.1,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API request failed");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API returned {Status}", response.StatusCode);
            return null;
        }

        var rawBody = await response.Content.ReadAsStringAsync(ct);
        return ParseAndValidate(rawBody);
    }

    private NlpExtractionResultDto? ParseAndValidate(string rawBody)
    {
        try
        {
            var root = JsonNode.Parse(rawBody);
            var text = root?["content"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Anthropic response missing text content");
                return null;
            }

            // Strip accidental markdown fences
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    text = text[(firstNewline + 1)..lastFence].Trim();
            }

            var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;

            var category = r.GetProperty("category").GetString() ?? "";
            var subcategory = r.GetProperty("subcategory").GetString() ?? "";
            var conditionLevel = r.TryGetProperty("condition_level", out var cl) ? cl.GetString() : null;
            var conditionConfidence = r.TryGetProperty("condition_confidence", out var ccEl) ? ccEl.GetDouble() : 0.5;

            // Backend validation: reject unknown categories
            if (!AllowedCategories.Contains(category))
            {
                _logger.LogWarning("NLP returned unknown category: {Category}", category);
                return null;
            }

            // Clamp confidence values
            conditionConfidence = Math.Clamp(conditionConfidence, 0.0, 1.0);

            var locEl = r.GetProperty("location");
            double locationConfidence = 0;
            if (locEl.TryGetProperty("location_confidence", out var lc))
                locationConfidence = Math.Clamp(lc.GetDouble(), 0.0, 1.0);

            var locationPrecisionType = locEl.TryGetProperty("location_precision_type", out var lpt)
                ? lpt.GetString()
                : null;
            var locationSource = locEl.TryGetProperty("location_source", out var ls)
                ? ls.GetString() ?? "nlp"
                : "nlp";

            var location = new NlpLocationDto(
                AreaName: GetStringOrNull(locEl, "area_name"),
                RoadName: GetStringOrNull(locEl, "road_name"),
                JunctionName: GetStringOrNull(locEl, "junction_name"),
                LandmarkName: GetStringOrNull(locEl, "landmark_name"),
                FacilityName: GetStringOrNull(locEl, "facility_name"),
                LocationLabel: GetStringOrNull(locEl, "location_label"),
                LocationPrecisionType: locationPrecisionType,
                LocationConfidence: locationConfidence,
                LocationSource: locationSource
            );

            NlpTemporalHintDto? temporalHint = null;
            if (r.TryGetProperty("temporal_hint", out var th))
            {
                var thType = th.TryGetProperty("type", out var tht) ? tht.GetString() ?? "episodic_unknown" : "episodic_unknown";
                if (!AllowedTemporalTypes.Contains(thType))
                    thType = "episodic_unknown";
                var thConf = th.TryGetProperty("confidence", out var thc) ? Math.Clamp(thc.GetDouble(), 0.0, 1.0) : 0.5;
                temporalHint = new NlpTemporalHintDto(thType, thConf);
            }

            var summary = r.TryGetProperty("summary", out var s) ? s.GetString() : null;
            var shouldSuggestJoin = r.TryGetProperty("should_suggest_join", out var ssj) && ssj.GetBoolean();

            List<string>? reasoningNotes = null;
            if (r.TryGetProperty("reasoning_notes", out var rn) && rn.ValueKind == JsonValueKind.Array)
                reasoningNotes = rn.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

            return new NlpExtractionResultDto(
                Category: category,
                Subcategory: subcategory,
                ConditionLevel: conditionLevel,
                ConditionConfidence: conditionConfidence,
                Location: location,
                TemporalHint: temporalHint,
                Summary: summary,
                ShouldSuggestJoin: shouldSuggestJoin,
                ReasoningNotes: reasoningNotes
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse NLP extraction response");
            return null;
        }
    }

    private static string? GetStringOrNull(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind != JsonValueKind.Null)
            return val.GetString();
        return null;
    }

    private static string BuildPrompt(NlpExtractionRequest request)
    {
        return $"""
            You are Hali CSI-NLP, a bounded civic signal extraction engine.

            Your task is to convert a user's free-text civic observation into structured JSON for a civic infrastructure product.

            Rules:
            1. Return JSON only.
            2. Do not include markdown.
            3. Do not explain yourself.
            4. Do not invent categories outside the allowed taxonomy.
            5. Use neutral language only.
            6. If location details are uncertain, set unknown fields to null and lower the confidence.
            7. Prefer specific human-readable location labels when enough evidence exists.
            8. Road-visible conditions should favor road/junction/landmark labeling.
            9. Utility conditions may use area-level labels if road-level detail is not present.
            10. Output must match the schema exactly.

            Allowed categories and subcategories:
            {request.TaxonomyBlock}

            Input context:
            - free_text: {request.FreeText}
            - user_latitude: {request.UserLatitude?.ToString() ?? "null"}
            - user_longitude: {request.UserLongitude?.ToString() ?? "null"}
            - selected_ward: {request.SelectedWard ?? "null"}
            - locale: {request.Locale ?? "null"}
            - current_time_utc: {request.CurrentTimeUtc}
            - known_city: {request.KnownCity ?? "null"}
            - country_code: {request.CountryCode ?? "null"}

            Return JSON only.
            """;
    }
}
