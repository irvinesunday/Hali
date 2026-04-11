using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Observability;

public class RoutingObservabilityTests
{
    private readonly IClusterRepository _repo = Substitute.For<IClusterRepository>();
    private readonly IH3CellService _h3 = Substitute.For<IH3CellService>();
    private readonly ICivisEvaluationService _civis = Substitute.For<ICivisEvaluationService>();
    private readonly RecordingLogger<ClusteringService> _logger = new();

    private ClusteringService CreateService(CivisOptions? opts = null)
    {
        var options = Options.Create(opts ?? new CivisOptions { JoinThreshold = 0.65, TimeScoreMaxAgeHours = 24.0 });
        return new ClusteringService(_repo, _h3, _civis, options, _logger);
    }

    private static SignalEvent MakeSignal(string spatialCellId = "8928308280fffff")
    {
        return new SignalEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Category = CivicCategory.Water,
            SubcategorySlug = "low-pressure",
            ConditionSlug = "no-water",
            NeutralSummary = "No water in the area.",
            TemporalType = "episodic",
            SpatialCellId = spatialCellId,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static SignalCluster MakeCluster(double lastSeenAgoHours = 0.5)
    {
        return new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = CivicCategory.Water,
            State = SignalState.Unconfirmed,
            SpatialCellId = "8928308280fffff",
            DominantConditionSlug = "no-water",
            RawConfirmationCount = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            UpdatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            FirstSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            LastSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours)
        };
    }

    [Fact]
    public async Task RouteSignal_JoinPath_EmitsRoutedEventWithJoinedOutcome()
    {
        var signal = MakeSignal();
        var cluster = MakeCluster();

        _h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        _repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { cluster });

        var svc = CreateService();
        await svc.RouteSignalAsync(signal);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.SignalRouted) && m.Contains("joined"));
    }

    [Fact]
    public async Task RouteSignal_CreatePath_EmitsRoutedEventWithCreatedOutcome()
    {
        var signal = MakeSignal();

        _h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        _repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        _repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<SignalCluster>()));

        var svc = CreateService();
        await svc.RouteSignalAsync(signal);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.SignalRouted) && m.Contains("created"));
    }

    [Fact]
    public async Task RouteSignal_RoutedEvent_IncludesCandidateCount()
    {
        var signal = MakeSignal();

        _h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        _repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        _repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<SignalCluster>()));

        var svc = CreateService();
        await svc.RouteSignalAsync(signal);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.SignalRouted) && m.Contains("candidateCount"));
    }

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
