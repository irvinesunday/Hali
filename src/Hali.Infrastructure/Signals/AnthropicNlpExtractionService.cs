using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Observability;
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

	private readonly SignalsMetrics? _metrics;

	private static readonly HashSet<string> AllowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "roads", "transport", "electricity", "water", "environment", "safety", "governance", "infrastructure" };

	private static readonly HashSet<string> AllowedTemporalTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temporary", "continuous", "recurring", "scheduled", "episodic_unknown" };

	public AnthropicNlpExtractionService(
		HttpClient http,
		IConfiguration config,
		ILogger<AnthropicNlpExtractionService> logger,
		SignalsMetrics? metrics = null)
	{
		_http = http;
		_apiKey = config["Anthropic:ApiKey"] ?? throw new InvalidOperationException("Anthropic:ApiKey is required");
		_model = config["Anthropic:Model"] ?? "claude-sonnet-4-6";
		_logger = logger;
		_metrics = metrics;
	}

	public async Task<NlpExtractionResultDto?> ExtractAsync(NlpExtractionRequest request, CancellationToken ct = default(CancellationToken))
	{
		// The histogram timer wraps the HTTP send + body-parse span only —
		// prompt construction is deterministic and sub-millisecond so
		// including it would muddy the "composer thinking time" signal the
		// metric is supposed to represent. The outcome tag is set to a
		// default of `fallback` and refined to `success` on a clean parse or
		// `timeout` if HttpClient surfaces a TaskCanceledException that was
		// not caused by the caller's cancellation token.
		string prompt = BuildPrompt(request);
		var body = new
		{
			model = _model,
			max_tokens = 1024,
			temperature = 0.1,
			messages = new[]
			{
				new
				{
					role = "user",
					content = prompt
				}
			}
		};
		using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
		req.Headers.Add("x-api-key", _apiKey);
		req.Headers.Add("anthropic-version", "2023-06-01");
		req.Content = JsonContent.Create(body);

		string outcome = SignalsMetrics.NlpOutcomeFallback;
		bool record = true;
		var sw = Stopwatch.StartNew();
		try
		{
			HttpResponseMessage response;
			try
			{
				response = await _http.SendAsync(req, ct);
			}
			catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
			{
				// HttpClient surfaces its own timeout as TaskCanceledException;
				// when the caller's CancellationToken was not signaled, the
				// cancellation came from the HttpClient timeout budget and
				// is operationally a timeout, not a generic fallback.
				outcome = SignalsMetrics.NlpOutcomeTimeout;
				_logger.LogError(ex, "Anthropic API request timed out");
				return null;
			}
			catch (OperationCanceledException)
			{
				// Caller cancellation — leave the histogram unrecorded so
				// dashboards do not conflate client disconnects with NLP
				// latency. Matches the behaviour of the request counters in
				// SignalsController.
				record = false;
				throw;
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
			var result = ParseAndValidate(await response.Content.ReadAsStringAsync(ct));
			if (result is not null)
			{
				outcome = SignalsMetrics.NlpOutcomeSuccess;
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			record = false;
			throw;
		}
		finally
		{
			sw.Stop();
			if (record)
			{
				_metrics?.NlpExtractionDuration.Record(
					sw.Elapsed.TotalSeconds,
					new KeyValuePair<string, object?>(SignalsMetrics.TagOutcome, outcome));
			}
		}
	}

	private NlpExtractionResultDto? ParseAndValidate(string rawBody)
	{
		try
		{
			string text = JsonNode.Parse(rawBody)?["content"]?[0]?["text"]?.GetValue<string>();
			if (string.IsNullOrWhiteSpace(text))
			{
				_logger.LogWarning("Anthropic response missing text content");
				return null;
			}
			text = text.Trim();
			if (text.StartsWith("```"))
			{
				int num = text.IndexOf('\n');
				int num2 = text.LastIndexOf("```");
				if (num > 0 && num2 > num)
				{
					string text2 = text;
					int num3 = num + 1;
					text = text2.Substring(num3, num2 - num3).Trim();
				}
			}
			JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			string text3 = rootElement.GetProperty("category").GetString() ?? "";
			string subcategory = rootElement.GetProperty("subcategory").GetString() ?? "";
			JsonElement value;
			string conditionLevel = (rootElement.TryGetProperty("condition_level", out value) ? value.GetString() : null);
			double value2 = (rootElement.TryGetProperty("condition_confidence", out var value3) ? value3.GetDouble() : 0.5);
			if (!AllowedCategories.Contains(text3))
			{
				_logger.LogWarning("NLP returned unknown category: {Category}", text3);
				return null;
			}
			value2 = Math.Clamp(value2, 0.0, 1.0);
			JsonElement property = rootElement.GetProperty("location");
			double locationConfidence = 0.0;
			if (property.TryGetProperty("location_confidence", out var value4))
			{
				locationConfidence = Math.Clamp(value4.GetDouble(), 0.0, 1.0);
			}
			JsonElement value5;
			string locationPrecisionType = (property.TryGetProperty("location_precision_type", out value5) ? value5.GetString() : null);
			JsonElement value6;
			string locationSource = (property.TryGetProperty("location_source", out value6) ? (value6.GetString() ?? "nlp") : "nlp");
			NlpLocationDto location = new NlpLocationDto(GetStringOrNull(property, "area_name"), GetStringOrNull(property, "road_name"), GetStringOrNull(property, "junction_name"), GetStringOrNull(property, "landmark_name"), GetStringOrNull(property, "facility_name"), GetStringOrNull(property, "location_label"), locationPrecisionType, locationConfidence, locationSource);
			NlpTemporalHintDto temporalHint = null;
			if (rootElement.TryGetProperty("temporal_hint", out var value7))
			{
				JsonElement value8;
				string text4 = (value7.TryGetProperty("type", out value8) ? (value8.GetString() ?? "episodic_unknown") : "episodic_unknown");
				if (!AllowedTemporalTypes.Contains(text4))
				{
					text4 = "episodic_unknown";
				}
				JsonElement value9;
				double confidence = (value7.TryGetProperty("confidence", out value9) ? Math.Clamp(value9.GetDouble(), 0.0, 1.0) : 0.5);
				temporalHint = new NlpTemporalHintDto(text4, confidence);
			}
			JsonElement value10;
			string summary = (rootElement.TryGetProperty("summary", out value10) ? value10.GetString() : null);
			JsonElement value11;
			bool shouldSuggestJoin = rootElement.TryGetProperty("should_suggest_join", out value11) && value11.GetBoolean();
			List<string> reasoningNotes = null;
			if (rootElement.TryGetProperty("reasoning_notes", out var value12) && value12.ValueKind == JsonValueKind.Array)
			{
				reasoningNotes = (from x in value12.EnumerateArray()
					select x.GetString() ?? "").ToList();
			}
			return new NlpExtractionResultDto(text3, subcategory, conditionLevel, value2, location, temporalHint, summary, shouldSuggestJoin, reasoningNotes);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to parse NLP extraction response");
			return null;
		}
	}

	private static string? GetStringOrNull(JsonElement el, string property)
	{
		if (el.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null)
		{
			return value.GetString();
		}
		return null;
	}

	private static string BuildPrompt(NlpExtractionRequest request)
	{
		return $"You are Hali CSI-NLP, a bounded civic signal extraction engine.\n\nYour task is to convert a user's free-text civic observation into structured JSON for a civic infrastructure product.\n\nRules:\n1. Return JSON only.\n2. Do not include markdown.\n3. Do not explain yourself.\n4. Do not invent categories outside the allowed taxonomy.\n5. Use neutral language only.\n6. If location details are uncertain, set unknown fields to null and lower the confidence.\n7. Prefer specific human-readable location labels when enough evidence exists.\n8. Road-visible conditions should favor road/junction/landmark labeling.\n9. Utility conditions may use area-level labels if road-level detail is not present.\n10. Output must match the schema exactly.\n\nAllowed categories and subcategories:\n{request.TaxonomyBlock}\n\nInput context:\n- free_text: {request.FreeText}\n- user_latitude: {(request.UserLatitude.HasValue ? request.UserLatitude.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) : "null")}\n- user_longitude: {(request.UserLongitude.HasValue ? request.UserLongitude.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) : "null")}\n- selected_ward: {request.SelectedWard ?? "null"}\n- locale: {request.Locale ?? "null"}\n- current_time_utc: {request.CurrentTimeUtc}\n- known_city: {request.KnownCity ?? "null"}\n- country_code: {request.CountryCode ?? "null"}\n\nReturn JSON only.";
	}
}
