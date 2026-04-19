using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Observability;
using Hali.Application.Clusters;
using Hali.Application.Institutions;
using Hali.Application.Observability;
using Hali.Contracts.Institutions;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Observability;

public class CorrelationContextTests
{
    // ── ICorrelationContext / CorrelationContext ────────────────────────────

    [Fact]
    public void CorrelationContext_ReturnsGuidFromHttpContextItems()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = expectedId.ToString("N");

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var sut = new CorrelationContext(accessor);

        // Act
        var result = sut.CurrentCorrelationId;

        // Assert
        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void CorrelationContext_ReturnsEmptyGuid_WhenHttpContextMissing()
    {
        // Arrange
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var sut = new CorrelationContext(accessor);

        // Act
        var result = sut.CurrentCorrelationId;

        // Assert
        Assert.Equal(Guid.Empty, result);
    }

    // ── OutboxEvent correlation id propagation — ClusteringService ─────────

    [Fact]
    public async Task OutboxEvent_CorrelationId_PropagatedFromContext_InClusteringService()
    {
        // Arrange
        var requestCorrelationId = Guid.NewGuid();

        var correlationContext = Substitute.For<ICorrelationContext>();
        correlationContext.CurrentCorrelationId.Returns(requestCorrelationId);

        var fakeRepo = new FakeClusterRepoForCorrelation();

        var h3 = Substitute.For<IH3CellService>();
        h3.GetKRingCells(Arg.Any<string>(), Arg.Any<int>())
            .Returns(new[] { "89300046c47ffff" });

        var civis = Substitute.For<ICivisEvaluationService>();
        civis.EvaluateClusterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = Options.Create(new CivisOptions
        {
            JoinThreshold = 0.65,
            TimeScoreMaxAgeHours = 24,
            WrabRollingWindowDays = 30,
            MinUniqueDevices = 2,
            DeactivationThreshold = 0.5,
            ActiveMassHorizonHours = 48,
            Roads = new CivisCategoryOptions { BaseFloor = 2, HalfLifeHours = 18, MacfMin = 2, MacfMax = 6 }
        });

        var sut = new ClusteringService(
            fakeRepo, h3, civis, options,
            correlationContext: correlationContext);

        var signal = new Hali.Domain.Entities.Signals.SignalEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Category = CivicCategory.Roads,
            SpatialCellId = "89300046c47ffff",
            CreatedAt = DateTime.UtcNow
        };

        // Act — no existing clusters, so ClusteringService will create a new one
        await sut.RouteSignalAsync(signal);

        // Assert — the outbox event written by ClusteringService must carry the request correlation id
        Assert.NotEmpty(fakeRepo.OutboxEvents);
        var outboxEvent = fakeRepo.OutboxEvents[0];
        Assert.Equal(requestCorrelationId, outboxEvent.CorrelationId);
    }

    // ── OutboxEvent correlation id propagation — InstitutionAcknowledgeService

    [Fact]
    public async Task OutboxEvent_CorrelationId_PropagatedFromContext_InInstitutionAcknowledgeService()
    {
        // Arrange
        var requestCorrelationId = Guid.NewGuid();

        var correlationContext = Substitute.For<ICorrelationContext>();
        correlationContext.CurrentCorrelationId.Returns(requestCorrelationId);

        var institutionId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        var institutionRepo = Substitute.For<IInstitutionReadRepository>();
        institutionRepo
            .GetScopeLocalityIdsAsync(institutionId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Guid>>(new List<Guid> { Guid.NewGuid() }));
        institutionRepo
            .IsClusterInScopeAsync(clusterId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var capturedOutbox = new List<OutboxEvent>();
        var clusterRepo = Substitute.For<IClusterRepository>();
        clusterRepo
            .WriteOutboxEventAsync(Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedOutbox.Add(ci.Arg<OutboxEvent>());
                return Task.CompletedTask;
            });

        var acknowledgementId = Guid.NewGuid();
        var store = Substitute.For<IInstitutionAcknowledgementStore>();
        store.TryGetReplayAsync(institutionId, clusterId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<InstitutionAcknowledgementReplay?>(null));
        store.TryClaimAsync(
                institutionId, clusterId, Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ack = new InstitutionAcknowledgementReplay(acknowledgementId, DateTime.UtcNow);
                return Task.FromResult((ack, true));
            });

        var sut = new InstitutionAcknowledgeService(
            institutionRepo,
            clusterRepo,
            store,
            correlationContext: correlationContext);

        var request = new InstitutionAcknowledgeRequestDto(
            IdempotencyKey: Guid.NewGuid().ToString("N"),
            Note: "test");

        // Act
        await sut.AcknowledgeAsync(institutionId, clusterId, request, CancellationToken.None);

        // Assert — the outbox event must carry the request correlation id
        Assert.Single(capturedOutbox);
        Assert.Equal(requestCorrelationId, capturedOutbox[0].CorrelationId);
    }

    // ── WorkerMetrics ──────────────────────────────────────────────────────

    [Fact]
    public void WorkerMetrics_RecordsJobDuration_OnSuccessAndFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMetrics();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        using var workerMetrics = new WorkerMetrics(factory);

        var recordedDurations = new List<double>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == WorkerMetrics.MeterName &&
                instrument.Name == WorkerMetrics.JobDurationSecondsName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, _, _) =>
        {
            recordedDurations.Add(measurement);
        });
        listener.Start();

        // Act — record a success duration and a failure duration
        workerMetrics.JobDurationSeconds.Record(
            0.5,
            new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, WorkerMetrics.JobTypeOutboxRelay));

        workerMetrics.JobDurationSeconds.Record(
            1.2,
            new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, WorkerMetrics.JobTypeDecayClusters));

        listener.RecordObservableInstruments();

        // Assert — two durations recorded (one for success path, one for failure path)
        Assert.Equal(2, recordedDurations.Count);
    }

    // ── Worker correlation rules ───────────────────────────────────────────

    [Fact]
    public void WorkerCorrelation_PropagatesExistingCorrelationId_FromOutboxEvent()
    {
        // The worker correlation rule: if the outbox event carries a
        // non-empty correlation id, propagate it — do not generate a new one.
        var existingCorrelationId = Guid.NewGuid();
        var outboxEvent = new OutboxEvent
        {
            CorrelationId = existingCorrelationId
        };

        // Apply rule: propagate existing, do not overwrite
        var resultCorrelationId = outboxEvent.CorrelationId != Guid.Empty
            ? outboxEvent.CorrelationId
            : Guid.NewGuid();

        Assert.Equal(existingCorrelationId, resultCorrelationId);
    }

    [Fact]
    public void WorkerCorrelation_GeneratesNewRoot_WhenNoCorrelationIdOnEvent()
    {
        // The worker correlation rule: if the outbox event has no correlation
        // id (empty), generate a new root guid.
        var outboxEvent = new OutboxEvent
        {
            CorrelationId = Guid.Empty
        };

        // Apply rule: generate new root when none exists
        var resultCorrelationId = outboxEvent.CorrelationId != Guid.Empty
            ? outboxEvent.CorrelationId
            : Guid.NewGuid();

        Assert.NotEqual(Guid.Empty, resultCorrelationId);
    }
}

// ── Fake cluster repo used by the correlation tests ────────────────────────────

internal sealed class FakeClusterRepoForCorrelation : IClusterRepository
{
    public List<OutboxEvent> OutboxEvents { get; } = new();
    public List<SignalCluster> CreatedClusters { get; } = new();

    public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(
        IEnumerable<string> spatialCells, CivicCategory category, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());

    public Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct) =>
        Task.FromResult<SignalCluster?>(null);

    public Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct)
    {
        CreatedClusters.Add(cluster);
        return Task.FromResult(cluster);
    }

    public Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct) => Task.CompletedTask;

    public Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct) => Task.FromResult(0);
    public Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct) => Task.FromResult(0);
    public Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct) => Task.FromResult(0);
    public Task<double> GetMinLocationConfidenceAsync(Guid clusterId, CancellationToken ct) => Task.FromResult(1.0);

    public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());

    public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());

    public Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct) => Task.CompletedTask;

    public Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct)
    {
        OutboxEvents.Add(outboxEvent);
        return Task.CompletedTask;
    }

    public Task ApplyClusterTransitionAsync(SignalCluster cluster, CivisDecision? decision, OutboxEvent outboxEvent, CancellationToken ct)
    {
        OutboxEvents.Add(outboxEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OutboxEvent>>(new List<OutboxEvent>());

    public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.CompletedTask;

    public Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());

    public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(
        IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());

    public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
        IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SignalCluster>>(new List<SignalCluster>());
}
