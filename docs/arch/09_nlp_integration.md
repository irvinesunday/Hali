# Hali — NLP Integration Guide (CSI-NLP)
**Anthropic Claude API integration for structured civic signal extraction.**

---

## Role and scope

CSI-NLP is a bounded extraction service, not a general AI assistant. Its only job is: free text → structured civic signal candidate. It must never generate prose, advice, or open-ended responses.

---

## Service interface

```csharp
public interface INlpExtractionService
{
    Task<NlpExtractionResult> ExtractAsync(
        NlpExtractionRequest request,
        CancellationToken ct = default);
}

public record NlpExtractionRequest(
    string Text,
    double? UserLatitude,
    double? UserLongitude,
    string? SelectedWard,
    string? Locale,
    DateTimeOffset CurrentTimeUtc,
    string? KnownCity,
    string CountryCode = "KE"
);
```

---

## Anthropic API call

```csharp
public class AnthropicNlpExtractionService : INlpExtractionService
{
    private readonly HttpClient http;
    private readonly NlpConfig config;

    public async Task<NlpExtractionResult> ExtractAsync(NlpExtractionRequest req, CancellationToken ct)
    {
        var taxonomyJson = taxonomyProvider.GetCanonicalTaxonomyJson();

        var systemPrompt = BuildSystemPrompt(taxonomyJson);
        var userPrompt   = BuildUserPrompt(req);

        var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", new
        {
            model       = config.Model,  // "claude-sonnet-4-5"
            max_tokens  = 1000,
            temperature = 0.1,           // low for consistency
            system      = systemPrompt,
            messages    = new[] { new { role = "user", content = userPrompt } }
        }, ct);

        var body = await response.Content.ReadFromJsonAsync<AnthropicResponse>(ct);
        var text = body.Content.First(c => c.Type == "text").Text;

        return ParseAndValidate(text);
    }

    private string BuildSystemPrompt(string taxonomyJson) => $"""
        You are Hali CSI-NLP, a bounded civic signal extraction engine.
        Your task is to convert a user's free-text civic observation into structured JSON.

        Rules:
        1. Return JSON only. No markdown. No preamble. No explanation.
        2. Do not invent categories outside the allowed taxonomy.
        3. Use neutral language only. No blame, no emotion, no political language.
        4. If location details are uncertain, set unknown fields to null and lower confidence.
        5. Prefer specific human-readable location labels when evidence exists.
        6. Road-visible conditions: favor road/junction/landmark labels.
        7. Utility conditions (water, electricity): area-level labels are acceptable.
        8. Output must match the schema exactly.

        Allowed taxonomy:
        {taxonomyJson}
        """;

    private string BuildUserPrompt(NlpExtractionRequest req) => $"""
        free_text: {req.Text}
        user_latitude: {req.UserLatitude?.ToString() ?? "null"}
        user_longitude: {req.UserLongitude?.ToString() ?? "null"}
        selected_ward: {req.SelectedWard ?? "null"}
        locale: {req.Locale ?? "en"}
        current_time_utc: {req.CurrentTimeUtc:O}
        known_city: {req.KnownCity ?? "Nairobi"}
        country_code: {req.CountryCode}

        Return JSON only.
        """;
}
```

---

## Expected JSON output schema

```json
{
  "category": "roads",
  "subcategory": "potholes",
  "condition_level": "difficult",
  "condition_confidence": 0.85,
  "location": {
    "area_name": "Nairobi West",
    "road_name": "Lusaka Road",
    "junction_name": "Lusaka Road / Uhuru Highway",
    "landmark_name": "CFAO Mobility",
    "facility_name": null,
    "location_label": "Potholes near CFAO Mobility at Lusaka Road / Uhuru Highway, Nairobi West",
    "location_precision_type": "road_landmark",
    "location_confidence": 0.82,
    "location_source": "nlp"
  },
  "temporal_hint": {
    "type": "temporary",
    "confidence": 0.64
  },
  "summary": "Potholes making the road difficult near CFAO Mobility on Lusaka Road, Nairobi West.",
  "should_suggest_join": true,
  "reasoning_notes": ["Detected road condition", "Detected landmark and junction"]
}
```

---

## Backend validation (mandatory — reject before persisting)

```csharp
private NlpExtractionResult ParseAndValidate(string jsonText)
{
    // Strip markdown fences if present (defense against model formatting)
    jsonText = jsonText.Trim().TrimStart('`').TrimEnd('`');
    if (jsonText.StartsWith("json")) jsonText = jsonText[4..].Trim();

    NlpRawOutput raw;
    try { raw = JsonSerializer.Deserialize<NlpRawOutput>(jsonText)!; }
    catch { throw new NlpExtractionException("Invalid JSON from NLP service"); }

    // Category must be in canonical enum
    if (!Enum.TryParse<CivicCategory>(raw.Category, ignoreCase: true, out var category))
        throw new NlpExtractionException($"Unknown category: {raw.Category}");

    // Subcategory must be registered in taxonomy
    if (!taxonomyValidator.IsValidSubcategory(category, raw.Subcategory))
        throw new NlpExtractionException($"Unknown subcategory: {raw.Subcategory} for {category}");

    // Clamp confidences
    var condConf = Math.Clamp(raw.ConditionConfidence, 0.0m, 1.0m);
    var locConf  = Math.Clamp(raw.Location.LocationConfidence, 0.0m, 1.0m);

    return new NlpExtractionResult(category, raw.Subcategory, raw.ConditionLevel,
        condConf, raw.Location, raw.TemporalHint, raw.Summary, raw.ShouldSuggestJoin,
        locConf);
}
```

---

## Confidence thresholds for UI routing

These values drive the Step 2 confirmation screen in the mobile app:

```
Location confidence:
  >= 0.80 → pre-fill, no mandatory confirm
  0.50–0.79 → show confirmation UI (yellow badge)
  < 0.50 → show empty field, mandatory user input

Condition confidence:
  >= 0.75 → accept as suggested
  0.50–0.74 → editable confirmation
  < 0.50 → treat as weak suggestion, show empty dropdown
```

---

## Taxonomy JSON injected into prompt

The `taxonomyProvider.GetCanonicalTaxonomyJson()` call generates this at runtime from the DB:

```json
{
  "categories": {
    "roads": ["potholes", "flooding", "obstruction", "road_damage", "impassable_section"],
    "transport": ["matatu_obstruction", "bus_stop_congestion", "lane_blocking", "access_disruption"],
    "electricity": ["outage", "unstable_supply", "transformer_issue"],
    "water": ["outage", "low_pressure", "burst_pipe", "sewage_issue"],
    "environment": ["illegal_dumping", "waste_overflow", "public_noise", "pollution"],
    "safety": ["exposed_hazard", "broken_streetlight", "unsafe_crossing"],
    "governance": ["public_service_disruption", "blocked_access_public_facility"],
    "infrastructure": ["broken_drainage", "damaged_footbridge", "damaged_public_asset"]
  }
}
```

Always inject from DB — do not hardcode in the prompt template. This ensures taxonomy changes propagate automatically.

---

## Error handling

```csharp
// If NLP call fails or times out:
//   - Log warning with signal_event_id
//   - Store raw text in signal_events.free_text (it is always stored)
//   - Return a fallback candidate with no category/subcategory
//   - Mobile app falls back to manual category picker in Step 2

// If NLP returns an unknown category:
//   - Reject the extraction
//   - Log extraction.category_rejected
//   - Return fallback candidate

// Never block signal submission because NLP failed.
// NLP enriches signals — it does not gate them.
```

---

## Performance

- NLP call timeout: 20 seconds (from `NLP_EXTRACTION_TIMEOUT_SECONDS` env var)
- Do not make NLP calls synchronously in the request path for signal submit
- The preview endpoint (`POST /v1/signals/preview`) calls NLP synchronously — this is expected
- The submit endpoint uses the candidate confirmed by the user — no second NLP call needed
- The NlpExtractWorker enriches the stored signal_events row asynchronously after submit (for analytics)

---

## What CSI-NLP must never do

- Generate open-ended responses or chat
- Invent categories not in the taxonomy
- Make political interpretations
- Assign blame or name responsible parties
- Return location data more precise than what the text supports
- Return confidence > 0.95 (cap at 0.95 — perfect confidence is not realistic)
