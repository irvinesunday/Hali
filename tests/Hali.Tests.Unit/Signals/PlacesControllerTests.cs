using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Auth;
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
    public async Task Search_QueryTooShort_ReturnsBadRequest(string? q)
    {
        PlacesController controller = CreateController();

        IActionResult result = await controller.Search(q!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Search_OversizedQuery_ReturnsBadRequest()
    {
        PlacesController controller = CreateController();
        string overlong = new string('x', 81);

        IActionResult result = await controller.Search(overlong, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
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
    public async Task Search_RateLimited_Returns429()
    {
        PlacesController controller = CreateController();
        // Override the default (allowed=true) *after* controller construction
        // so this per-test setup wins.
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await controller.Search("Ngong Road", CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, obj.StatusCode);
        // Must not fan out to the upstream geocoder once we've decided to throttle.
        await _geocoding.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- reverse ----

    [Theory]
    [InlineData(-91.0, 36.8)]
    [InlineData(91.0, 36.8)]
    [InlineData(-1.3, -181.0)]
    [InlineData(-1.3, 181.0)]
    public async Task Reverse_InvalidCoordinates_ReturnsBadRequest(double lat, double lng)
    {
        PlacesController controller = CreateController();

        IActionResult result = await controller.Reverse(lat, lng, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Reverse_PointOutsideKnownLocality_Returns404()
    {
        _localities.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);

        PlacesController controller = CreateController();
        IActionResult result = await controller.Reverse(-1.3, 36.8, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
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
    public async Task Reverse_RateLimited_Returns429()
    {
        PlacesController controller = CreateController();
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await controller.Reverse(-1.3, 36.8, CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, obj.StatusCode);
    }
}
