using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Signals;

public class PlacesControllerTests
{
    private readonly IGeocodingService _geocoding = Substitute.For<IGeocodingService>();
    private readonly ILocalityLookupRepository _localities = Substitute.For<ILocalityLookupRepository>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();
    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();

    private static readonly Guid LocalityA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LocalityB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private PlacesController CreateController(string clientIp = "203.0.113.1")
    {
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
        _redis.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcOptions =
            Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions());

        var controller = new PlacesController(
            _geocoding,
            _localities,
            _redis,
            _rateLimiter,
            mvcOptions,
            NullLogger<PlacesController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    // ---- search ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task Search_QueryTooShort_ThrowsValidationException(string? q)
    {
        PlacesController controller = CreateController();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => controller.Search(q!, CancellationToken.None));
        Assert.Equal("places.query_too_short", ex.Code);
    }

    [Fact]
    public async Task Search_OversizedQuery_ThrowsValidationException()
    {
        PlacesController controller = CreateController();
        string overlong = new string('x', 81);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => controller.Search(overlong, CancellationToken.None));
        Assert.Equal("places.query_too_long", ex.Code);
    }

    [Fact]
    public async Task Search_HappyPath_ReturnsResolvedCandidates()
    {
        _geocoding.SearchAsync("Ngong Road", Arg.Any<CancellationToken>())
            .Returns(new List<GeocodingCandidate>
            {
                new GeocodingCandidate("Ngong Road, Nairobi, Kenya", -1.30, 36.78),
                new GeocodingCandidate("Ngong Forest, Nairobi, Kenya", -1.31, 36.77),
            });
        _localities.FindByPointAsync(-1.30, 36.78, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));
        _localities.FindByPointAsync(-1.31, 36.77, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityB, "Dagoretti", "Nairobi", "Nairobi"));

        PlacesController controller = CreateController();
        IActionResult result = await controller.Search("Ngong Road", CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        List<PlaceCandidateDto> payload = Assert.IsAssignableFrom<IEnumerable<PlaceCandidateDto>>(ok.Value).ToList();
        Assert.Equal(2, payload.Count);
        Assert.Equal(LocalityA, payload[0].LocalityId);
        Assert.Equal("Nairobi West", payload[0].WardName);
        Assert.Equal(-1.30, payload[0].Latitude);
        Assert.Equal(36.78, payload[0].Longitude);
        // Display-name is trimmed to first two comma-separated segments.
        Assert.Equal("Ngong Road, Nairobi", payload[0].DisplayName);
    }

    [Fact]
    public async Task Search_CandidatesOutsideKnownLocalities_AreFilteredOut()
    {
        _geocoding.SearchAsync("Somewhere", Arg.Any<CancellationToken>())
            .Returns(new List<GeocodingCandidate>
            {
                new GeocodingCandidate("Ocean, International Waters", 0.0, 0.0),
                new GeocodingCandidate("Ngong Road, Nairobi", -1.30, 36.78),
            });
        _localities.FindByPointAsync(0.0, 0.0, Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);
        _localities.FindByPointAsync(-1.30, 36.78, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));

        PlacesController controller = CreateController();
        IActionResult result = await controller.Search("Somewhere", CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        List<PlaceCandidateDto> payload = Assert.IsAssignableFrom<IEnumerable<PlaceCandidateDto>>(ok.Value).ToList();
        PlaceCandidateDto only = Assert.Single(payload);
        Assert.Equal(LocalityA, only.LocalityId);
    }

    [Fact]
    public async Task Search_GeocodingThrows_FailsClosedWithEmptyList()
    {
        _geocoding.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GeocodingCandidate>>(_ => throw new InvalidOperationException("boom"));

        PlacesController controller = CreateController();
        IActionResult result = await controller.Search("Ngong Road", CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        List<PlaceCandidateDto> payload = Assert.IsAssignableFrom<IEnumerable<PlaceCandidateDto>>(ok.Value).ToList();
        Assert.Empty(payload);
    }

    [Fact]
    public async Task Search_RateLimited_ThrowsRateLimitException()
    {
        PlacesController controller = CreateController();
        // Override the default (allowed=true) *after* controller construction
        // so this per-test setup wins.
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var ex = await Assert.ThrowsAsync<RateLimitException>(
            () => controller.Search("Ngong Road", CancellationToken.None));
        Assert.Equal("places.search_rate_limited", ex.Code);
        // Must not fan out to the upstream geocoder once we've decided to throttle.
        await _geocoding.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- reverse ----

    [Theory]
    [InlineData(-91.0, 36.8)]
    [InlineData(91.0, 36.8)]
    [InlineData(-1.3, -181.0)]
    [InlineData(-1.3, 181.0)]
    public async Task Reverse_InvalidCoordinates_ThrowsValidationException(double lat, double lng)
    {
        PlacesController controller = CreateController();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => controller.Reverse(lat, lng, CancellationToken.None));
        // H3 (#153): `places.invalid_coordinates` collapsed to the canonical
        // `validation.invalid_coordinates` alongside the parallel
        // `locality.invalid_coordinates` site.
        Assert.Equal(ErrorCodes.ValidationInvalidCoordinates, ex.Code);
    }

    [Fact]
    public async Task Reverse_PointOutsideKnownLocality_ThrowsNotFoundException()
    {
        _localities.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);

        PlacesController controller = CreateController();
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => controller.Reverse(-1.3, 36.8, CancellationToken.None));
        // H3 (#153): `places.locality_not_found` collapsed to the canonical
        // `locality.not_found` (same concept as the locality-resolve endpoint).
        Assert.Equal(ErrorCodes.LocalityNotFound, ex.Code);
        await _geocoding.DidNotReceive().ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reverse_HappyPath_ReturnsCandidateWithNominatimLabel()
    {
        _localities.FindByPointAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns(new GeocodingResult("Ngong Road, Nairobi West, Nairobi, Kenya", "Ngong Road", "Nairobi West", "Nairobi", "Kenya"));

        PlacesController controller = CreateController();
        IActionResult result = await controller.Reverse(-1.3, 36.8, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        PlaceCandidateDto payload = Assert.IsType<PlaceCandidateDto>(ok.Value);
        Assert.Equal("Ngong Road, Nairobi West", payload.DisplayName);
        Assert.Equal(LocalityA, payload.LocalityId);
        Assert.Equal(-1.3, payload.Latitude);
        Assert.Equal(36.8, payload.Longitude);
    }

    [Fact]
    public async Task Reverse_ReverseGeocodeReturnsNull_FallsBackToWardLabel()
    {
        _localities.FindByPointAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns((GeocodingResult?)null);

        PlacesController controller = CreateController();
        IActionResult result = await controller.Reverse(-1.3, 36.8, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        PlaceCandidateDto payload = Assert.IsType<PlaceCandidateDto>(ok.Value);
        // Fallback label is "WardName, CityName" when reverse geocoding is
        // unavailable — keeps the label civic-friendly even on Nominatim failure.
        Assert.Equal("Nairobi West, Nairobi", payload.DisplayName);
    }

    [Fact]
    public async Task Reverse_ReverseGeocodeThrows_FallsBackToWardLabel()
    {
        _localities.FindByPointAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(-1.3, 36.8, Arg.Any<CancellationToken>())
            .Returns<GeocodingResult?>(_ => throw new InvalidOperationException("nominatim down"));

        PlacesController controller = CreateController();
        IActionResult result = await controller.Reverse(-1.3, 36.8, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        PlaceCandidateDto payload = Assert.IsType<PlaceCandidateDto>(ok.Value);
        Assert.Equal("Nairobi West, Nairobi", payload.DisplayName);
    }

    [Fact]
    public async Task Reverse_RateLimited_ThrowsRateLimitException()
    {
        PlacesController controller = CreateController();
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var ex = await Assert.ThrowsAsync<RateLimitException>(
            () => controller.Reverse(-1.3, 36.8, CancellationToken.None));
        Assert.Equal("places.reverse_rate_limited", ex.Code);
    }

    // ---- reverse cache integrity (B-2 / Copilot #1) ----
    //
    // Previously the reverse cache was keyed only by quantized coords
    // ({lat:F4}:{lng:F4}) and stored the full PlaceCandidateDto. Two
    // callers whose points rounded into the same 4dp bucket could share
    // a response, which across ward boundaries — or across the edge of
    // known coverage — leaked a neighbor's localityId / wardName / coords
    // and could bypass the locality guard entirely.
    //
    // The fix: always call FindByPointAsync for the caller's coordinates;
    // the cache now holds ONLY the expensive Nominatim label, keyed by
    // {lat:F4}:{lng:F4}:{localityId} so a same-bucket cross-locality
    // request cannot reuse a neighbour's label.
    //
    // These tests prove the two integrity invariants without relying on
    // NSubstitute's StringSetAsync overload resolution (which is brittle
    // across SE.Redis 2.12.x's multiple default-arg overloads of
    // StringSetAsync): we simulate "a prior call has populated the
    // cache" by pre-seeding `StringGetAsync` with a payload that the old
    // design would have returned, and assert that the new design ignores
    // it and re-resolves the caller's locality + rebuilds the response.

    [Fact]
    public async Task Reverse_OutsideLocality_DoesNotConsultCacheOrUpstream()
    {
        // Simulate: a prior same-bucket call populated the cache with a
        // valid candidate in LocalityA. Now a caller whose point is
        // OUTSIDE any known locality hits the same bucket.
        //
        // Old design: StringGetAsync would have returned the cached DTO
        // and the endpoint would have skipped FindByPointAsync entirely,
        // returning LocalityA's data → locality guard bypassed.
        //
        // New design: FindByPointAsync is always called first; when it
        // returns null the cache is never consulted and 404 is returned.
        PlaceCandidateDto seededNeighbour = new PlaceCandidateDto
        {
            DisplayName = "Ngong Road, Nairobi West",
            Latitude = -1.30001,
            Longitude = 36.80001,
            LocalityId = LocalityA,
            WardName = "Nairobi West",
            CityName = "Nairobi",
        };
        string seededJson = JsonSerializer.Serialize(seededNeighbour);
        _localities.FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);

        PlacesController controller = CreateController();
        // Seed StringGetAsync AFTER CreateController so this setup wins
        // over the default "always return RedisValue.Null" setup.
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(seededJson);

        await Assert.ThrowsAsync<NotFoundException>(
            () => controller.Reverse(-1.30004, 36.80004, CancellationToken.None));
        // Integrity assertions: FindByPointAsync ran for the caller's
        // coords, and BOTH downstream dependencies (cache + upstream
        // geocoder) were never touched — we short-circuit at the locality
        // guard. The Redis seeding above is a trap: if the controller
        // ever consulted the cache for an outside-locality request, it
        // would receive the seeded payload and behave incorrectly. The
        // `DidNotReceive` assertion on `StringGetAsync` is what closes
        // the trap; without it this test would still pass even if the
        // controller were silently reading the cache before the locality
        // check.
        await _localities.Received(1)
            .FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>());
        await _redis.DidNotReceive()
            .StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
        await _geocoding.DidNotReceive()
            .ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reverse_CrossLocalitySameBucket_DoesNotReturnNeighborLocality()
    {
        // Simulate: a prior same-bucket call in LocalityA populated the
        // cache. Now a caller whose point is inside LocalityB (different
        // ward, same 4dp bucket) arrives.
        //
        // Old design: StringGetAsync returned LocalityA's DTO verbatim,
        // so callerB saw LocalityA's wardName/cityName/localityId + caller
        // A's coords.
        //
        // New design: the cache key is partitioned by localityId, so a
        // seeded entry written under LocalityA's key is not visible to a
        // lookup under LocalityB's key. The controller calls Nominatim
        // fresh for callerB and returns callerB's own locality.
        PlaceCandidateDto seededLocalityA = new PlaceCandidateDto
        {
            DisplayName = "Ngong Road, Nairobi West",
            Latitude = -1.30001,
            Longitude = 36.80001,
            LocalityId = LocalityA,
            WardName = "Nairobi West",
            CityName = "Nairobi",
        };
        string seededJson = JsonSerializer.Serialize(seededLocalityA);

        _localities.FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityB, "Dagoretti", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>())
            .Returns(new GeocodingResult("Ngong Road, Dagoretti, Nairobi, Kenya",
                "Ngong Road", "Dagoretti", "Nairobi", "Kenya"));

        PlacesController controller = CreateController();
        // Seed StringGetAsync to return the LocalityA payload only when
        // an old-style key (no localityId in the key) is queried — to
        // prove that the new code never uses that key. Any key the
        // new controller asks for (which includes localityId) returns
        // RedisValue.Null → cache miss → live Nominatim call.
        _redis.StringGetAsync(
                Arg.Is<RedisKey>(k =>
                    k.ToString().StartsWith("places_reverse:", StringComparison.Ordinal)
                    && !k.ToString().StartsWith("places_reverse_label:", StringComparison.Ordinal)),
                Arg.Any<CommandFlags>())
            .Returns(seededJson);

        IActionResult result = await controller.Reverse(-1.30004, 36.80004, CancellationToken.None);

        PlaceCandidateDto payload = Assert.IsType<PlaceCandidateDto>(Assert.IsType<OkObjectResult>(result).Value);
        // CallerB's response reflects CallerB's own locality + coords,
        // NOT LocalityA from the seeded neighbour payload.
        Assert.Equal(LocalityB, payload.LocalityId);
        Assert.Equal("Dagoretti", payload.WardName);
        Assert.Equal(-1.30004, payload.Latitude);
        Assert.Equal(36.80004, payload.Longitude);
        Assert.Equal("Ngong Road, Dagoretti", payload.DisplayName);

        await _localities.Received(1)
            .FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reverse_CacheKey_IncludesLocalityId()
    {
        // Proves the cache partitioning: the controller's cache read uses
        // a key that contains the resolved localityId. This is the
        // structural guarantee that a cross-locality same-bucket request
        // cannot reuse a neighbour's cache entry.
        _localities.FindByPointAsync(-1.30001, 36.80001, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityA, "Nairobi West", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((GeocodingResult?)null);

        PlacesController controller = CreateController();
        IActionResult result = await controller.Reverse(-1.30001, 36.80001, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        // At least one StringGetAsync call must have used a key that
        // contains the resolved localityId, anchoring the cache to the
        // caller's actual locality rather than coords alone.
        await _redis.Received().StringGetAsync(
            Arg.Is<RedisKey>(k =>
                k.ToString().StartsWith("places_reverse_label:", StringComparison.Ordinal)
                && k.ToString().Contains(LocalityA.ToString())),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Reverse_AlwaysResolvesCallerLocality_EvenWhenSomethingIsCached()
    {
        // Strongest invariant: FindByPointAsync runs for the caller's
        // coords BEFORE any cached payload can affect the response. Pre-
        // seed a cache hit that would satisfy any key, and assert the
        // locality repository was still consulted with the caller's coords.
        PlaceCandidateDto seeded = new PlaceCandidateDto
        {
            DisplayName = "Seeded Label",
            Latitude = 0.0,
            Longitude = 0.0,
            LocalityId = LocalityA,
            WardName = "Seeded Ward",
            CityName = "Seeded City",
        };
        string seededJson = JsonSerializer.Serialize(seeded);
        _localities.FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(LocalityB, "Dagoretti", "Nairobi", "Nairobi"));
        _geocoding.ReverseGeocodeAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((GeocodingResult?)null);

        PlacesController controller = CreateController();
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(seededJson);

        IActionResult result = await controller.Reverse(-1.30004, 36.80004, CancellationToken.None);

        PlaceCandidateDto payload = Assert.IsType<PlaceCandidateDto>(Assert.IsType<OkObjectResult>(result).Value);
        // Locality came from the fresh lookup, not the seeded cache entry.
        Assert.Equal(LocalityB, payload.LocalityId);
        Assert.Equal(-1.30004, payload.Latitude);
        Assert.Equal(36.80004, payload.Longitude);
        await _localities.Received(1)
            .FindByPointAsync(-1.30004, 36.80004, Arg.Any<CancellationToken>());
    }
}
