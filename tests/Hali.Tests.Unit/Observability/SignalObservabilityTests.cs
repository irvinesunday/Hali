using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Hali.Tests.Unit.Observability;

public class SignalObservabilityTests
{
    private readonly INlpExtractionService _nlp = Substitute.For<INlpExtractionService>();
    private readonly ISignalRepository _repo = Substitute.For<ISignalRepository>();
    private readonly IClusteringService _clustering = Substitute.For<IClusteringService>();
    private readonly IH3CellService _h3 = Substitute.For<IH3CellService>();
    private readonly ILocalityLookupRepository _localityLookup = Substitute.For<ILocalityLookupRepository>();
    private readonly RecordingLogger<SignalIngestionService> _logger = new();

    private static readonly Guid DefaultLocalityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid DefaultClusterId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");

    private SignalIngestionService CreateService()
    {
        return new SignalIngestionService(_nlp, _repo, _clustering, _h3, _localityLookup, _logger);
    }

    private static SignalSubmitRequestDto MakeSubmitRequest(string idempKey = "key-abc")
    {
        return new SignalSubmitRequestDto(idempKey, "device-hash-1", "Big potholes on Lusaka Road",
            "roads", "potholes", "difficult", 0.85, -1.3, 36.8,
            "Potholes on Lusaka Road", "road", 0.8, "nlp",
            "temporary", "Potholes on Lusaka Road.", "en");
    }

    private void SetupHappyPath()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Returns("892a1008003ffff");
        _localityLookup.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new LocalitySummary(DefaultLocalityId, "Nairobi West", "Nairobi", "Nairobi"));
        _repo.PersistSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.ArgAt<SignalEvent>(0);
            return new SignalEvent { Id = s.Id, CreatedAt = s.CreatedAt, OccurredAt = s.OccurredAt, Category = s.Category, SpatialCellId = s.SpatialCellId, LocalityId = s.LocalityId };
        });
        _clustering.RouteSignalAsync(Arg.Any<SignalEvent>(), Arg.Any<CancellationToken>())
            .Returns(new ClusterRoutingResult(DefaultClusterId, WasCreated: true, WasJoined: false, "unconfirmed", DefaultLocalityId));
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_EmitsStartedAndCompletedEvents()
    {
        SetupHappyPath();
        var svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalSubmitStarted));
        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalSubmitCompleted));
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_EmitsSpatialDerivedEvent()
    {
        SetupHappyPath();
        var svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalSpatialDerived));
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_EmitsLocalityResolvedEvent()
    {
        SetupHappyPath();
        var svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalLocalityResolved));
    }

    [Fact]
    public async Task SubmitAsync_CompletedEvent_IncludesDurationMs()
    {
        SetupHappyPath();
        var svc = CreateService();

        await svc.SubmitAsync(MakeSubmitRequest(), null, null);

        Assert.Contains(_logger.Messages,
            m => m.Contains(ObservabilityEvents.SignalSubmitCompleted) && m.Contains("durationMs"));
    }

    [Fact]
    public async Task SubmitAsync_InvalidCategory_EmitsFailedEvent()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var request = MakeSubmitRequest() with { Category = "aliens" };
        var svc = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(request, null, null));

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalSubmitFailed));
    }

    [Fact]
    public async Task SubmitAsync_LocalityUnresolved_EmitsLocalityFailedEvent()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Returns("892a1008003ffff");
        _localityLookup.FindByPointAsync(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns((LocalitySummary?)null);
        var svc = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalLocalityFailed));
    }

    [Fact]
    public async Task SubmitAsync_SpatialDerivationFails_EmitsSpatialFailedEvent()
    {
        _repo.IdempotencyKeyExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.IsRateLimitAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _h3.LatLngToCell(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>()).Returns("");
        var svc = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(MakeSubmitRequest(), null, null));

        Assert.Contains(_logger.Messages, m => m.Contains(ObservabilityEvents.SignalSpatialFailed));
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
