using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Home;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Notifications;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Observability;

public class HomeObservabilityTests
{
    private readonly IHomeFeedQueryService _feedQuery = Substitute.For<IHomeFeedQueryService>();
    private readonly IFollowService _follows = Substitute.For<IFollowService>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();
    private readonly RecordingLogger<HomeController> _logger = new();

    private HomeController CreateController(bool authenticated = true, Guid? accountId = null)
    {
        var controller = new HomeController(
            _feedQuery,
            _follows,
            _redis,
            Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions()),
            _logger);
        var context = new DefaultHttpContext();
        if (authenticated)
        {
            var aid = accountId ?? Guid.NewGuid();
            var claims = new[]
            {
                new System.Security.Claims.Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    aid.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "test");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static readonly Guid LocalityId = Guid.NewGuid();

    private void SetupDefaultFeed()
    {
        _follows.GetFollowedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Follow> { new() { LocalityId = LocalityId } });

        _feedQuery.GetActiveByLocalitiesPagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<bool?>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());

        _feedQuery.GetAllActivePagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());

        _feedQuery.GetOfficialPostsByLocalitiesAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OfficialPostResponseDto>());

        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);
    }

    [Fact]
    public async Task GetHome_EmitsStartedAndCompletedEvents()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeRequestStarted));
        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeRequestCompleted));
    }

    [Fact]
    public async Task GetHome_WithExplicitLocality_EmitsExplicitScopeEvent()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, LocalityId, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeLocalityScopeExplicit));
    }

    [Fact]
    public async Task GetHome_WithFollowedLocalities_EmitsFallbackScopeEvent()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeLocalityScopeFallback));
    }

    [Fact]
    public async Task GetHome_GuestUser_EmitsGuestEmptyScopeEvent()
    {
        SetupDefaultFeed();
        var controller = CreateController(authenticated: false);

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeLocalityScopeGuestEmpty));
    }

    [Fact]
    public async Task GetHome_CacheMiss_EmitsCacheCheckedAndMissEvents()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeCacheChecked));
        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeCacheMiss));
    }

    [Fact]
    public async Task GetHome_CacheHit_EmitsCacheHitEvent()
    {
        _follows.GetFollowedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Follow> { new() { LocalityId = LocalityId } });
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("{}"));

        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeCacheHit));
    }

    [Fact]
    public async Task GetHome_FullResponse_EmitsSectionBuiltEventsForAllSections()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeSectionBuilt) && m.Contains("active_now"));
        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeSectionBuilt) && m.Contains("official_updates"));
        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeSectionBuilt) && m.Contains("recurring_at_this_time"));
        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeSectionBuilt) && m.Contains("other_active_signals"));
    }

    [Fact]
    public async Task GetHome_CompletedEvent_IncludesDurationMs()
    {
        SetupDefaultFeed();
        var controller = CreateController();

        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.HomeRequestCompleted) && m.Contains("durationMs"));
    }

    /// <summary>
    /// Minimal ILogger that captures formatted messages for assertion.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
