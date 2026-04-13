using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Api.Serialization;
using Hali.Application.Home;
using Hali.Application.Notifications;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Notifications;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Contracts;

/// <summary>
/// Contract drift prevention tests.
///
/// The serializer options used by these tests are resolved from an
/// <see cref="IServiceCollection"/> configured with
/// <see cref="ApiJsonConfiguration.Configure"/> — the exact same delegate
/// <c>Program.cs</c> passes to <c>AddJsonOptions</c>. Any future change to
/// Program.cs's JSON config propagates here automatically; there is no
/// locally-maintained copy that could silently drift.
///
/// If any of these tests fail after a code change, the OpenAPI spec or DTO
/// must be updated to keep contract and implementation in sync.
/// </summary>
public class ContractDriftTests
{
    /// <summary>
    /// The MVC-configured JSON serializer options, built from the shared
    /// <see cref="ApiJsonConfiguration.Configure"/> delegate. This is the
    /// same options instance HomeController's cache-write path consumes
    /// via <c>IOptions&lt;JsonOptions&gt;</c> in production.
    /// </summary>
    private static readonly JsonSerializerOptions ApiJsonOptions = BuildApiJsonOptions();

    private static JsonSerializerOptions BuildApiJsonOptions()
    {
        var services = new ServiceCollection();
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(ApiJsonConfiguration.Configure);
        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions;
    }

    private static IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> BuildMvcJsonOptions()
    {
        var services = new ServiceCollection();
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(ApiJsonConfiguration.Configure);
        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>();
    }

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
    public void HomeResponseDto_DefaultOptions_DivergeFromApiOptions_ProvingOptionsMatter()
    {
        // This test guards against DRIFT-2: the Redis cache path must not use
        // default JsonSerializerOptions, because they diverge from the MVC
        // serializer configured in Program.cs. HomeController now sources its
        // cache serializer from IOptions<JsonOptions> (MVC config), but this
        // test locks in the contract that default options WOULD produce a
        // different shape — so any accidental revert would be caught here
        // and by HomeController_WritesCacheJson_UsingMvcConfiguredSerializer.
        var dto = new HomeResponseDto
        {
            ActiveNow = new PagedSection<ClusterResponseDto>
            {
                Items = new[] { MakeClusterResponse() },
                NextCursor = null,
                TotalCount = 1
            }
        };

        // Default options: PascalCase properties (drift case)
        var defaultJson = JsonSerializer.Serialize(dto);
        // API options: camelCase properties (correct case)
        var apiJson = JsonSerializer.Serialize(dto, ApiJsonOptions);

        Assert.NotEqual(defaultJson, apiJson);

        // Default options would leak PascalCase
        using (var doc = JsonDocument.Parse(defaultJson))
        {
            Assert.True(doc.RootElement.TryGetProperty("ActiveNow", out _));
            Assert.False(doc.RootElement.TryGetProperty("activeNow", out _));
        }

        // API options produce camelCase as the contract requires
        using (var doc = JsonDocument.Parse(apiJson))
        {
            Assert.True(doc.RootElement.TryGetProperty("activeNow", out var section));
            Assert.True(section.TryGetProperty("items", out _));
            Assert.True(section.TryGetProperty("nextCursor", out _));
            Assert.True(section.TryGetProperty("totalCount", out _));
        }
    }

    [Fact]
    public async Task HomeController_WritesCacheJson_UsingMvcConfiguredSerializer()
    {
        // Drives HomeController through its real cache-write branch and
        // asserts the JSON written to Redis uses the MVC-configured
        // camelCase property naming and snake_case enum serialization.
        //
        // Guards against C12 DRIFT-2 regressing: if the cache serializer
        // ever used default JsonSerializerOptions again (PascalCase + raw
        // enum names), the same endpoint would return different shapes on
        // cache hits vs misses.

        var feedQuery = Substitute.For<IHomeFeedQueryService>();
        var follows = Substitute.For<IFollowService>();
        var redis = Substitute.For<IDatabase>();

        var localityId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        follows.GetFollowedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Follow> { new() { LocalityId = localityId } });

        // One active cluster with the multi-word PossibleRestoration state
        // so the test exercises both camelCase property naming and the
        // snake_case enum contract (possible_restoration).
        var cluster = new SignalCluster
        {
            Id = Guid.NewGuid(),
            LocalityId = localityId,
            Category = CivicCategory.Water,
            SubcategorySlug = "water_outage",
            State = SignalState.PossibleRestoration,
            Title = "Water outage",
            Summary = "No water since 6am",
            AffectedCount = 5,
            ObservingCount = 2,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow.AddHours(-1),
        };

        feedQuery.GetActiveByLocalitiesPagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<bool?>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                // Return the cluster for non-recurring; empty for recurring.
                var recurringOnly = ci.ArgAt<bool?>(1);
                return (IReadOnlyList<SignalCluster>)(recurringOnly == true
                    ? Array.Empty<SignalCluster>()
                    : new[] { cluster });
            });
        feedQuery.GetAllActivePagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        feedQuery.GetOfficialPostsByLocalityAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<OfficialPostResponseDto>)Array.Empty<OfficialPostResponseDto>());

        // Cache miss on read, capture the JSON on write. The controller's
        // 3-arg StringSetAsync(key, value, TimeSpan?) call is dispatched by
        // the C# compiler to the newer StackExchange.Redis overload that
        // takes (RedisKey, RedisValue, Expiration, ValueCondition, CommandFlags),
        // so the mock must match that signature.
        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);
        string? capturedJson = null;
        redis.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Do<RedisValue>(v => capturedJson = (string?)v),
                Arg.Any<Expiration>(),
                Arg.Any<ValueCondition>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        var controller = new HomeController(
            feedQuery,
            follows,
            redis,
            BuildMvcJsonOptions(),
            NullLogger<HomeController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = AuthenticatedHttpContext(accountId)
        };

        await controller.GetHome(section: null, cursor: null, localityId: null, CancellationToken.None);

        Assert.NotNull(capturedJson);
        using var doc = JsonDocument.Parse(capturedJson!);
        var root = doc.RootElement;

        // camelCase section naming
        Assert.True(root.TryGetProperty("activeNow", out var activeNow));
        Assert.False(root.TryGetProperty("ActiveNow", out _));

        // snake_case enum value on the cluster state
        var firstItem = activeNow.GetProperty("items")[0];
        Assert.Equal("possible_restoration", firstItem.GetProperty("state").GetString());

        // camelCase field on the cluster
        Assert.True(firstItem.TryGetProperty("affectedCount", out _));
        Assert.False(firstItem.TryGetProperty("AffectedCount", out _));
    }

    private static HttpContext AuthenticatedHttpContext(Guid accountId)
    {
        var ctx = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                accountId.ToString())
        };
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return ctx;
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
        // SignalState via JsonNamingPolicy.SnakeCaseLower.ConvertName —
        // identical to the global JsonStringEnumConverter naming policy.
        // Verify the exact wire values match the OpenAPI enum array:
        // [unconfirmed, active, possible_restoration, resolved].
        var state = Enum.Parse<Hali.Domain.Enums.SignalState>(
            expected.Replace("_", ""), ignoreCase: true);
        string serialized = JsonNamingPolicy.SnakeCaseLower.ConvertName(state.ToString());
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
}
