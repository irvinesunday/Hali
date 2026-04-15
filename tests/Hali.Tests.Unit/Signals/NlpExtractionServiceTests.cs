using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hali.Application.Signals;
using Hali.Infrastructure.Signals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hali.Tests.Unit.Signals;

public class NlpExtractionServiceTests
{
	private static AnthropicNlpExtractionService CreateService(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
	{
		FakeHttpMessageHandler handler = new FakeHttpMessageHandler(status, responseBody);
		HttpClient http = new HttpClient(handler);
		IConfigurationRoot config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
		{
			["Anthropic:ApiKey"] = "test-key",
			["Anthropic:Model"] = "claude-sonnet-4-6"
		}).Build();
		return new AnthropicNlpExtractionService(http, config, NullLogger<AnthropicNlpExtractionService>.Instance);
	}

	private static NlpExtractionRequest MakeRequest(string freeText = "Big potholes near National Oil in Nairobi West")
	{
		return new NlpExtractionRequest(freeText, -1.3, 36.8, null, "en", "Nairobi", "KE", DateTime.UtcNow.ToString("o"), "roads: potholes, flooding");
	}

	[Fact]
	public async Task ExtractAsync_HappyPath_ReturnsValidResult()
	{
		string nlpJson = "{\n  \"category\": \"roads\",\n  \"subcategory\": \"potholes\",\n  \"condition_level\": \"difficult\",\n  \"condition_confidence\": 0.85,\n  \"location\": {\n    \"area_name\": \"Nairobi West\",\n    \"road_name\": null,\n    \"junction_name\": null,\n    \"landmark_name\": \"National Oil\",\n    \"facility_name\": null,\n    \"location_label\": \"Potholes near National Oil, Nairobi West\",\n    \"location_precision_type\": \"road_landmark\",\n    \"location_confidence\": 0.80,\n    \"location_source\": \"nlp\"\n  },\n  \"temporal_hint\": { \"type\": \"temporary\", \"confidence\": 0.70 },\n  \"summary\": \"Potholes reported near National Oil in Nairobi West.\",\n  \"should_suggest_join\": true,\n  \"reasoning_notes\": [\"Detected road condition\", \"Detected landmark\"]\n}";
		string anthropicResponse = WrapInAnthropicResponse(nlpJson);
		AnthropicNlpExtractionService svc = CreateService(anthropicResponse);
		NlpExtractionResultDto? result = await svc.ExtractAsync(MakeRequest());
		Assert.NotNull(result);
		Assert.Equal("roads", result.Category);
		Assert.Equal("potholes", result.Subcategory);
		Assert.Equal("difficult", result.ConditionLevel);
		Assert.Equal(0.85, result.ConditionConfidence, 2);
		Assert.Equal("Nairobi West", result.Location.AreaName);
		Assert.Equal("National Oil", result.Location.LandmarkName);
		Assert.Equal("temporary", result.TemporalHint?.Type);
		Assert.True(result.ShouldSuggestJoin);
		Assert.Equal(2, result.ReasoningNotes?.Count);
	}

	[Fact]
	public async Task ExtractAsync_MalformedJson_ReturnsNull()
	{
		string anthropicResponse = WrapInAnthropicResponse("This is not JSON at all, just prose.");
		AnthropicNlpExtractionService svc = CreateService(anthropicResponse);
		Assert.Null(await svc.ExtractAsync(MakeRequest()));
	}

	[Fact]
	public async Task ExtractAsync_UnknownCategory_ReturnsNull()
	{
		string nlpJson = "{\n  \"category\": \"aliens\",\n  \"subcategory\": \"abduction\",\n  \"condition_level\": \"severe\",\n  \"condition_confidence\": 0.99,\n  \"location\": {\n    \"location_confidence\": 0.5,\n    \"location_source\": \"nlp\"\n  },\n  \"summary\": \"Aliens landed.\",\n  \"should_suggest_join\": false\n}";
		string anthropicResponse = WrapInAnthropicResponse(nlpJson);
		AnthropicNlpExtractionService svc = CreateService(anthropicResponse);
		Assert.Null(await svc.ExtractAsync(MakeRequest()));
	}

	[Fact]
	public async Task ExtractAsync_ApiReturnsError_ReturnsNull()
	{
		AnthropicNlpExtractionService svc = CreateService("{}", HttpStatusCode.InternalServerError);
		Assert.Null(await svc.ExtractAsync(MakeRequest()));
	}

	[Fact]
	public async Task ExtractAsync_ConfidenceOutOfRange_IsClamped()
	{
		string nlpJson = "{\n  \"category\": \"water\",\n  \"subcategory\": \"outage\",\n  \"condition_level\": \"none\",\n  \"condition_confidence\": 1.5,\n  \"location\": {\n    \"location_confidence\": -0.2,\n    \"location_source\": \"nlp\"\n  },\n  \"summary\": \"No water in South B.\",\n  \"should_suggest_join\": false\n}";
		string anthropicResponse = WrapInAnthropicResponse(nlpJson);
		AnthropicNlpExtractionService svc = CreateService(anthropicResponse);
		NlpExtractionResultDto? result = await svc.ExtractAsync(MakeRequest("No water in South B"));
		Assert.NotNull(result);
		Assert.Equal(1.0, result.ConditionConfidence);
		Assert.Equal(0.0, result.Location.LocationConfidence);
	}

	[Fact]
	public async Task ExtractAsync_ResponseWrappedInMarkdownFences_IsHandled()
	{
		string nlpJson = "```json\n{\n  \"category\": \"electricity\",\n  \"subcategory\": \"outage\",\n  \"condition_level\": \"complete_outage\",\n  \"condition_confidence\": 0.90,\n  \"location\": {\n    \"area_name\": \"South B\",\n    \"location_confidence\": 0.75,\n    \"location_source\": \"nlp\"\n  },\n  \"summary\": \"Power outage in South B.\",\n  \"should_suggest_join\": true\n}\n```";
		string anthropicResponse = WrapInAnthropicResponse(nlpJson);
		AnthropicNlpExtractionService svc = CreateService(anthropicResponse);
		NlpExtractionResultDto? result = await svc.ExtractAsync(MakeRequest("No power in South B"));
		Assert.NotNull(result);
		Assert.Equal("electricity", result.Category);
	}

	private static string WrapInAnthropicResponse(string text)
	{
		string text2 = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")
			.Replace("\r", "");
		return "{\"content\":[{\"type\":\"text\",\"text\":\"" + text2 + "\"}]}";
	}
}
