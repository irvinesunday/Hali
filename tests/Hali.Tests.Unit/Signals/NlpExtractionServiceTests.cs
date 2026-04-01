using System.Net;
using System.Text;
using Hali.Application.Signals;
using Hali.Infrastructure.Signals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Hali.Tests.Unit.Signals;

public class NlpExtractionServiceTests
{
    private static AnthropicNlpExtractionService CreateService(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(status, responseBody);
        var http = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "test-key",
                ["Anthropic:Model"] = "claude-sonnet-4-6"
            })
            .Build();

        return new AnthropicNlpExtractionService(http, config, NullLogger<AnthropicNlpExtractionService>.Instance);
    }

    private static NlpExtractionRequest MakeRequest(string freeText = "Big potholes near National Oil in Nairobi West") =>
        new(freeText, -1.3, 36.8, null, "en", "Nairobi", "KE", DateTime.UtcNow.ToString("o"), "roads: potholes, flooding");

    [Fact]
    public async Task ExtractAsync_HappyPath_ReturnsValidResult()
    {
        var nlpJson = """
            {
              "category": "roads",
              "subcategory": "potholes",
              "condition_level": "difficult",
              "condition_confidence": 0.85,
              "location": {
                "area_name": "Nairobi West",
                "road_name": null,
                "junction_name": null,
                "landmark_name": "National Oil",
                "facility_name": null,
                "location_label": "Potholes near National Oil, Nairobi West",
                "location_precision_type": "road_landmark",
                "location_confidence": 0.80,
                "location_source": "nlp"
              },
              "temporal_hint": { "type": "temporary", "confidence": 0.70 },
              "summary": "Potholes reported near National Oil in Nairobi West.",
              "should_suggest_join": true,
              "reasoning_notes": ["Detected road condition", "Detected landmark"]
            }
            """;

        var anthropicResponse = WrapInAnthropicResponse(nlpJson);
        var svc = CreateService(anthropicResponse);

        var result = await svc.ExtractAsync(MakeRequest());

        Assert.NotNull(result);
        Assert.Equal("roads", result.Category);
        Assert.Equal("potholes", result.Subcategory);
        Assert.Equal("difficult", result.ConditionLevel);
        Assert.Equal(0.85, result.ConditionConfidence, precision: 2);
        Assert.Equal("Nairobi West", result.Location.AreaName);
        Assert.Equal("National Oil", result.Location.LandmarkName);
        Assert.Equal("temporary", result.TemporalHint?.Type);
        Assert.True(result.ShouldSuggestJoin);
        Assert.Equal(2, result.ReasoningNotes?.Count);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ReturnsNull()
    {
        var anthropicResponse = WrapInAnthropicResponse("This is not JSON at all, just prose.");
        var svc = CreateService(anthropicResponse);

        var result = await svc.ExtractAsync(MakeRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_UnknownCategory_ReturnsNull()
    {
        var nlpJson = """
            {
              "category": "aliens",
              "subcategory": "abduction",
              "condition_level": "severe",
              "condition_confidence": 0.99,
              "location": {
                "location_confidence": 0.5,
                "location_source": "nlp"
              },
              "summary": "Aliens landed.",
              "should_suggest_join": false
            }
            """;

        var anthropicResponse = WrapInAnthropicResponse(nlpJson);
        var svc = CreateService(anthropicResponse);

        var result = await svc.ExtractAsync(MakeRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_ApiReturnsError_ReturnsNull()
    {
        var svc = CreateService("{}", HttpStatusCode.InternalServerError);

        var result = await svc.ExtractAsync(MakeRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractAsync_ConfidenceOutOfRange_IsClamped()
    {
        var nlpJson = """
            {
              "category": "water",
              "subcategory": "outage",
              "condition_level": "none",
              "condition_confidence": 1.5,
              "location": {
                "location_confidence": -0.2,
                "location_source": "nlp"
              },
              "summary": "No water in South B.",
              "should_suggest_join": false
            }
            """;

        var anthropicResponse = WrapInAnthropicResponse(nlpJson);
        var svc = CreateService(anthropicResponse);

        var result = await svc.ExtractAsync(MakeRequest("No water in South B"));

        Assert.NotNull(result);
        Assert.Equal(1.0, result.ConditionConfidence);
        Assert.Equal(0.0, result.Location.LocationConfidence);
    }

    [Fact]
    public async Task ExtractAsync_ResponseWrappedInMarkdownFences_IsHandled()
    {
        var nlpJson = """
            ```json
            {
              "category": "electricity",
              "subcategory": "outage",
              "condition_level": "complete_outage",
              "condition_confidence": 0.90,
              "location": {
                "area_name": "South B",
                "location_confidence": 0.75,
                "location_source": "nlp"
              },
              "summary": "Power outage in South B.",
              "should_suggest_join": true
            }
            ```
            """;

        var anthropicResponse = WrapInAnthropicResponse(nlpJson);
        var svc = CreateService(anthropicResponse);

        var result = await svc.ExtractAsync(MakeRequest("No power in South B"));

        Assert.NotNull(result);
        Assert.Equal("electricity", result.Category);
    }

    private static string WrapInAnthropicResponse(string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        return $$"""{"content":[{"type":"text","text":"{{escaped}}"}]}""";
    }
}

/// <summary>Fake HTTP handler for unit tests — does not make real network calls.</summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public FakeHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
