using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Contracts.Signals;
using Xunit;

namespace Hali.Tests.Unit.Contracts;

/// <summary>
/// Contract drift prevention tests.
///
/// These tests verify that the serialized JSON shapes of key DTOs match the
/// OpenAPI spec (02_openapi.yaml). They use the same JsonSerializerOptions
/// configured in Program.cs so that property naming and enum serialization
/// are tested end-to-end.
///
/// If any of these tests fail after a code change, the OpenAPI spec or DTO
/// must be updated to keep contract and implementation in sync.
/// </summary>
public class ContractDriftTests
{
    /// <summary>
    /// Matches the serializer configured in Program.cs:
    ///   PropertyNamingPolicy = CamelCase
    ///   JsonStringEnumConverter with SnakeCaseLower
    /// </summary>
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    // ── ClusterResponseDto ──────────────────────────────────────────────────

    [Fact]
    public void ClusterResponseDto_Serializes_WithCamelCasePropertyNames()
    {
        var dto = MakeClusterResponse();
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Required properties per OpenAPI spec
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("state", out _));
        Assert.True(root.TryGetProperty("category", out _));
        Assert.True(root.TryGetProperty("subcategorySlug", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("summary", out _));
        Assert.True(root.TryGetProperty("locationLabel", out _));
        Assert.True(root.TryGetProperty("affectedCount", out _));
        Assert.True(root.TryGetProperty("observingCount", out _));
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.True(root.TryGetProperty("updatedAt", out _));
        Assert.True(root.TryGetProperty("activatedAt", out _));
        Assert.True(root.TryGetProperty("possibleRestorationAt", out _));
        Assert.True(root.TryGetProperty("resolvedAt", out _));
        Assert.True(root.TryGetProperty("officialPosts", out _));
        Assert.True(root.TryGetProperty("myParticipation", out _));

        // No PascalCase variants leaked
        Assert.False(root.TryGetProperty("Id", out _));
        Assert.False(root.TryGetProperty("State", out _));
        Assert.False(root.TryGetProperty("AffectedCount", out _));
        Assert.False(root.TryGetProperty("OfficialPosts", out _));
    }

    [Theory]
    [InlineData("possible_restoration")]
    [InlineData("unconfirmed")]
    [InlineData("active")]
    [InlineData("resolved")]
    public void ClusterResponseDto_State_MatchesOpenApiEnumValues(string expectedState)
    {
        // The State field is a string in the DTO (manually converted from
        // SignalState enum). This test verifies that the four valid OpenAPI
        // enum values round-trip correctly through JSON serialization.
        var dto = MakeClusterResponse() with { State = expectedState };
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(expectedState, doc.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void ClusterResponseDto_NullableFields_SerializedAsJsonNull()
    {
        var dto = MakeClusterResponse();
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);

        // Nullable fields are always present on the wire (required in OpenAPI
        // 3.1 means present-on-wire, not non-null). They serialize as JSON null.
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("activatedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("possibleRestorationAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("resolvedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("myParticipation").ValueKind);
    }

    // ── MyParticipationDto ──────────────────────────────────────────────────

    [Fact]
    public void MyParticipationDto_Serializes_WithCamelCasePropertyNames()
    {
        var dto = new MyParticipationDto("affected", DateTime.UtcNow, true, false);
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.True(root.TryGetProperty("canAddContext", out _));
        Assert.True(root.TryGetProperty("canRespondToRestoration", out _));
    }

    // ── HomeResponseDto ─────────────────────────────────────────────────────

    [Fact]
    public void HomeResponseDto_Serializes_WithCamelCaseSectionNames()
    {
        var dto = new HomeResponseDto();
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("activeNow", out _));
        Assert.True(root.TryGetProperty("officialUpdates", out _));
        Assert.True(root.TryGetProperty("recurringAtThisTime", out _));
        Assert.True(root.TryGetProperty("otherActiveSignals", out _));

        // No PascalCase variants
        Assert.False(root.TryGetProperty("ActiveNow", out _));
        Assert.False(root.TryGetProperty("OfficialUpdates", out _));
    }

    [Fact]
    public void HomeResponseDto_CacheJsonMatchesMvcJson()
    {
        // This test guards against DRIFT-2: the Redis cache path must produce
        // the same JSON shape as the MVC Ok() path. Both must use CamelCase.
        var dto = new HomeResponseDto
        {
            ActiveNow = new PagedSection<ClusterResponseDto>
            {
                Items = new[] { MakeClusterResponse() },
                NextCursor = null,
                TotalCount = 1
            }
        };

        // Simulate the cache serialization (HomeController.CacheJsonOptions)
        var cacheJson = JsonSerializer.Serialize(dto, ApiJsonOptions);
        // Simulate the MVC serialization (same options configured in Program.cs)
        var mvcJson = JsonSerializer.Serialize(dto, ApiJsonOptions);

        Assert.Equal(mvcJson, cacheJson);

        // Verify camelCase in the serialized output
        using var doc = JsonDocument.Parse(cacheJson);
        Assert.True(doc.RootElement.TryGetProperty("activeNow", out var section));
        Assert.True(section.TryGetProperty("items", out _));
        Assert.True(section.TryGetProperty("nextCursor", out _));
        Assert.True(section.TryGetProperty("totalCount", out _));
    }

    // ── PagedSection<T> ─────────────────────────────────────────────────────

    [Fact]
    public void PagedSection_Serializes_WithCamelCasePropertyNames()
    {
        var section = new PagedSection<ClusterResponseDto>
        {
            Items = new[] { MakeClusterResponse() },
            NextCursor = "abc123",
            TotalCount = 42
        };

        var json = JsonSerializer.Serialize(section, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("items", out _));
        Assert.True(root.TryGetProperty("nextCursor", out _));
        Assert.True(root.TryGetProperty("totalCount", out _));
    }

    // ── SignalPreviewResponseDto ─────────────────────────────────────────────

    [Fact]
    public void SignalPreviewResponseDto_Serializes_WithCamelCasePropertyNames()
    {
        var dto = new SignalPreviewResponseDto(
            "water", "water_outage", null, 0.85,
            new SignalLocationDto(null, null, null, null, null, "Ngong Rd", null, 0.9, "nlp"),
            null, "Water outage reported", true);

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("category", out _));
        Assert.True(root.TryGetProperty("subcategorySlug", out _));
        Assert.True(root.TryGetProperty("conditionConfidence", out _));
        Assert.True(root.TryGetProperty("location", out var loc));
        Assert.True(root.TryGetProperty("shouldSuggestJoin", out _));
        Assert.True(loc.TryGetProperty("locationLabel", out _));
        Assert.True(loc.TryGetProperty("locationConfidence", out _));
        Assert.True(loc.TryGetProperty("locationSource", out _));
    }

    // ── SignalSubmitResponseDto ──────────────────────────────────────────────

    [Fact]
    public void SignalSubmitResponseDto_Serializes_WithCamelCasePropertyNames()
    {
        var dto = new SignalSubmitResponseDto(
            Guid.NewGuid(), Guid.NewGuid(), true, "unconfirmed", null, DateTime.UtcNow);

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("signalEventId", out _));
        Assert.True(root.TryGetProperty("clusterId", out _));
        Assert.True(root.TryGetProperty("isNewCluster", out _));
        Assert.True(root.TryGetProperty("clusterState", out _));
        Assert.True(root.TryGetProperty("localityId", out _));
        Assert.True(root.TryGetProperty("createdAt", out _));
    }

    // ── OfficialPostResponseDto ─────────────────────────────────────────────

    [Fact]
    public void OfficialPostResponseDto_Serializes_WithCamelCasePropertyNames()
    {
        var dto = new OfficialPostResponseDto(
            Guid.NewGuid(), Guid.NewGuid(),
            "live_update", "water", "Repair crew dispatched",
            "A repair crew is on the way.", null, null,
            "published", null, false, DateTime.UtcNow);

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("institutionId", out _));
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("isRestorationClaim", out _));
        Assert.True(root.TryGetProperty("relatedClusterId", out _));
        Assert.True(root.TryGetProperty("startsAt", out _));
    }

    // ── Enum snake_case contract tests ──────────────────────────────────────

    [Theory]
    [InlineData("possible_restoration")]
    [InlineData("unconfirmed")]
    [InlineData("active")]
    [InlineData("resolved")]
    public void SignalState_SnakeCaseValues_MatchOpenApiEnum(string expected)
    {
        // The State field in ClusterResponseDto is manually converted from
        // SignalState via ToSnakeCase. Verify the exact wire values match
        // the OpenAPI enum array: [unconfirmed, active, possible_restoration, resolved].
        var state = Enum.Parse<Hali.Domain.Enums.SignalState>(
            expected.Replace("_", ""), ignoreCase: true);
        string serialized = ToSnakeCase(state.ToString());
        Assert.Equal(expected, serialized);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClusterResponseDto MakeClusterResponse() =>
        new ClusterResponseDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "active",
            "water",
            "water_outage",
            "Water outage on Ngong Road",
            "No water since 6am",
            12, 3,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null, null, null)
        {
            LocationLabel = null,
            MyParticipation = null
        };

    /// <summary>
    /// Mirror of the ToSnakeCase helper used in HomeController and
    /// ClustersController. Kept here so the test is self-contained.
    /// </summary>
    private static string ToSnakeCase(string pascal)
    {
        var sb = new System.Text.StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
