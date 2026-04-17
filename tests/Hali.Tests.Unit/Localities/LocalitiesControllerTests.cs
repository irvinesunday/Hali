using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Auth;
using Hali.Application.Notifications;
using Hali.Application.Signals;
using Hali.Contracts.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Localities;

/// <summary>
/// Narrow unit coverage for the catch-all around
/// <c>LocalitiesController.Search</c>'s geocoding call. Issue #155 required
/// that <see cref="OperationCanceledException"/> NOT be absorbed into a
/// fail-closed empty 200 response; ordinary exceptions must still produce
/// that fail-closed behavior unchanged.
/// </summary>
public class LocalitiesControllerTests
{
    private readonly IFollowService _follows = Substitute.For<IFollowService>();
    private readonly IGeocodingService _geocoding = Substitute.For<IGeocodingService>();
    private readonly ILocalityLookupRepository _localities = Substitute.For<ILocalityLookupRepository>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();
    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();

    private LocalitiesController CreateController(string clientIp = "203.0.113.1")
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

        var controller = new LocalitiesController(
            _follows,
            _geocoding,
            _localities,
            _redis,
            _rateLimiter,
            mvcOptions,
            NullLogger<LocalitiesController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    [Fact]
    public async Task Search_GeocodingCanceled_PropagatesOperationCanceledException()
    {
        // OperationCanceledException must NOT be absorbed into an empty 200;
        // the catch-all filter lets it flow to the framework so
        // ExceptionHandlingMiddleware's client-disconnect path handles it.
        _geocoding.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GeocodingCandidate>>(_ => throw new OperationCanceledException());

        LocalitiesController controller = CreateController();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => controller.Search("Ngong Road", CancellationToken.None));
    }

    [Fact]
    public async Task Search_GeocodingThrows_FailsClosedWithEmptyList()
    {
        // Ordinary (non-cancellation) exceptions must continue to fail closed
        // with an empty 200 — the filter must not change this behavior.
        _geocoding.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GeocodingCandidate>>(_ => throw new InvalidOperationException("boom"));

        LocalitiesController controller = CreateController();
        IActionResult result = await controller.Search("Ngong Road", CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        IEnumerable<LocalitySearchResultDto> payload =
            Assert.IsAssignableFrom<IEnumerable<LocalitySearchResultDto>>(ok.Value);
        Assert.Empty(payload);
    }
}
