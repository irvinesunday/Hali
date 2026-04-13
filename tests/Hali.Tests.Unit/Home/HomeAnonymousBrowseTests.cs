using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Home;
using Hali.Application.Notifications;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Notifications;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Home;

/// <summary>
/// Tests verifying B8 anonymous browse behavior on the HomeController.
/// Anonymous users can pass ?localityId to scope the feed.
/// </summary>
public class HomeAnonymousBrowseTests
{
    private static readonly Guid TestLocalityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid TestClusterId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly IHomeFeedQueryService _feedQuery = Substitute.For<IHomeFeedQueryService>();
    private readonly IFollowService _follows = Substitute.For<IFollowService>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();

    private HomeController CreateController(ClaimsPrincipal? user = null)
    {
        var controller = new HomeController(
            _feedQuery,
            _follows,
            _redis,
            Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions()),
            NullLogger<HomeController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    private static ClaimsPrincipal AuthenticatedUser(Guid accountId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private void SetupEmptyFeed()
    {
        _feedQuery
            .GetActiveByLocalitiesPagedAsync(
                Arg.Any<List<Guid>>(), Arg.Any<bool>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>().AsReadOnly());

        _feedQuery
            .GetAllActivePagedAsync(
                Arg.Any<List<Guid>>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>().AsReadOnly());

        _feedQuery
            .GetOfficialPostsByLocalityAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Hali.Contracts.Advisories.OfficialPostResponseDto>());
    }

    private static SignalCluster MakeCluster(Guid id, Guid localityId) =>
        new()
        {
            Id = id,
            LocalityId = localityId,
            Category = CivicCategory.Roads,
            SubcategorySlug = "pothole",
            State = SignalState.Active,
            Title = "Test cluster",
            Summary = "Test",
            AffectedCount = 2,
            ObservingCount = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow.AddMinutes(-30),
        };

    // ── Anonymous with localityId ─────────────────────────────────────

    [Fact]
    public async Task GetHome_AnonymousWithLocalityId_ReturnsFeed()
    {
        // Arrange — anonymous user (no auth claims), passing ?localityId
        var controller = CreateController();
        var clusters = new List<SignalCluster> { MakeCluster(TestClusterId, TestLocalityId) };

        _feedQuery
            .GetActiveByLocalitiesPagedAsync(
                Arg.Is<List<Guid>>(l => l.Contains(TestLocalityId)),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(clusters.AsReadOnly());

        _feedQuery
            .GetAllActivePagedAsync(
                Arg.Any<List<Guid>>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>().AsReadOnly());

        _feedQuery
            .GetOfficialPostsByLocalityAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Hali.Contracts.Advisories.OfficialPostResponseDto>());

        // Act
        var result = await controller.GetHome(
            section: null, cursor: null, localityId: TestLocalityId, ct: CancellationToken.None);

        // Assert — 200 OK with feed data
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var feed = Assert.IsType<HomeResponseDto>(ok.Value);
        Assert.Single(feed.ActiveNow.Items);
    }

    [Fact]
    public async Task GetHome_AnonymousWithoutLocalityId_ReturnsEmptySections()
    {
        // Arrange — anonymous user, no localityId
        var controller = CreateController();

        // Act
        var result = await controller.GetHome(
            section: null, cursor: null, localityId: null, ct: CancellationToken.None);

        // Assert — all four sections are empty (no followed localities for guest)
        var ok = Assert.IsType<OkObjectResult>(result);
        var feed = Assert.IsType<HomeResponseDto>(ok.Value);
        Assert.Empty(feed.ActiveNow.Items);
        Assert.Empty(feed.OfficialUpdates.Items);
        Assert.Empty(feed.RecurringAtThisTime.Items);
        Assert.Empty(feed.OtherActiveSignals.Items);
    }

    // ── Anonymous cache behavior ───────────────────────────────────

    [Fact]
    public async Task GetHome_AnonymousWithLocalityId_DoesNotWriteToCache()
    {
        // Arrange — anonymous user, cache miss
        var controller = CreateController();

        SetupEmptyFeed();

        // Act
        await controller.GetHome(
            section: null, cursor: null, localityId: TestLocalityId, ct: CancellationToken.None);

        // Assert — no StringSetAsync call was made to Redis.
        // Uses ReceivedCalls() to avoid NSubstitute overload resolution issues
        // with StackExchange.Redis 2.12's multiple StringSetAsync overloads.
        var redisCalls = _redis.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StringSetAsync")
            .ToList();
        Assert.Empty(redisCalls);
    }

    [Fact]
    public async Task GetHome_AuthenticatedWithLocalityId_WritesToCache()
    {
        // Arrange — authenticated user, cache miss
        var accountId = Guid.NewGuid();
        var controller = CreateController(AuthenticatedUser(accountId));

        SetupEmptyFeed();

        // Act
        await controller.GetHome(
            section: null, cursor: null, localityId: TestLocalityId, ct: CancellationToken.None);

        // Assert — exactly one StringSetAsync call was made to Redis.
        // NSubstitute's ReceivedCalls() captures all calls regardless of overload.
        var redisCalls = _redis.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StringSetAsync")
            .ToList();
        Assert.Single(redisCalls);
    }

    // ── Authenticated still works ────────────────────────────────────

    [Fact]
    public async Task GetHome_AuthenticatedWithLocalityId_ReturnsFeed()
    {
        // Arrange — authenticated user passing ?localityId
        var accountId = Guid.NewGuid();
        var controller = CreateController(AuthenticatedUser(accountId));
        var clusters = new List<SignalCluster> { MakeCluster(TestClusterId, TestLocalityId) };

        _feedQuery
            .GetActiveByLocalitiesPagedAsync(
                Arg.Is<List<Guid>>(l => l.Contains(TestLocalityId)),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(clusters.AsReadOnly());

        _feedQuery
            .GetAllActivePagedAsync(
                Arg.Any<List<Guid>>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>().AsReadOnly());

        _feedQuery
            .GetOfficialPostsByLocalityAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Hali.Contracts.Advisories.OfficialPostResponseDto>());

        // Act
        var result = await controller.GetHome(
            section: null, cursor: null, localityId: TestLocalityId, ct: CancellationToken.None);

        // Assert — still returns 200 with feed
        var ok = Assert.IsType<OkObjectResult>(result);
        var feed = Assert.IsType<HomeResponseDto>(ok.Value);
        Assert.Single(feed.ActiveNow.Items);
    }

    [Fact]
    public async Task GetHome_AuthenticatedWithoutLocalityId_FallsBackToFollowed()
    {
        // Arrange — authenticated user, no localityId, has followed localities
        var accountId = Guid.NewGuid();
        var controller = CreateController(AuthenticatedUser(accountId));

        _follows
            .GetFollowedAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<Follow>
            {
                new() { Id = Guid.NewGuid(), AccountId = accountId, LocalityId = TestLocalityId, CreatedAt = DateTime.UtcNow }
            });

        var clusters = new List<SignalCluster> { MakeCluster(TestClusterId, TestLocalityId) };

        _feedQuery
            .GetActiveByLocalitiesPagedAsync(
                Arg.Is<List<Guid>>(l => l.Contains(TestLocalityId)),
                Arg.Any<bool>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(clusters.AsReadOnly());

        _feedQuery
            .GetAllActivePagedAsync(
                Arg.Any<List<Guid>>(),
                Arg.Any<int>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>().AsReadOnly());

        _feedQuery
            .GetOfficialPostsByLocalityAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Hali.Contracts.Advisories.OfficialPostResponseDto>());

        // Act
        var result = await controller.GetHome(
            section: null, cursor: null, localityId: null, ct: CancellationToken.None);

        // Assert — feeds from followed localities
        var ok = Assert.IsType<OkObjectResult>(result);
        var feed = Assert.IsType<HomeResponseDto>(ok.Value);
        Assert.Single(feed.ActiveNow.Items);

        // Verify follows were queried
        await _follows.Received(1).GetFollowedAsync(accountId, Arg.Any<CancellationToken>());
    }
}
