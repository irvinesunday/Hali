using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Hali.Infrastructure.Signals;
using Hali.Tests.Unit.Clusters;
using Hali.Tests.Unit.Signals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Verifies that the signals pipeline emits the four instruments owned by
/// <see cref="SignalsMetrics"/>:
/// <list type="bullet">
///   <item><description><c>signals_preview_requests_total</c> /
///     <c>signals_submit_requests_total</c> — increment exactly once per
///     controller call with a bounded outcome tag and no request-derived
///     dimensions;</description></item>
///   <item><description><c>nlp_extraction_duration_seconds</c> — records
///     once per completed NLP call on the success, fallback, and timeout
///     paths (and stays silent on caller-cancellation);</description></item>
///   <item><description><c>signal_join_outcome_total</c> — emits
///     <c>joined_existing</c> or <c>created_new</c> at the clustering
///     decision point, plus <c>activated_cluster</c> when CIVIS flips the
///     cluster to Active.</description></item>
/// </list>
///
/// Each test owns an isolated <see cref="SignalsMetrics"/> via
/// <see cref="TestSignalsMetrics"/> so the <see cref="MeterListener"/> only
/// observes measurements from that test's meter — keeping the suite
/// parallel-safe.
/// </summary>
public class SignalsMetricsTests
{
    private sealed record DoubleMeasurement(double Value, Dictionary<string, object?> Tags);
    private sealed record LongMeasurement(long Value, Dictionary<string, object?> Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<DoubleMeasurement> NlpDurationMeasurements { get; } = new();
        public List<LongMeasurement> PreviewMeasurements { get; } = new();
        public List<LongMeasurement> SubmitMeasurements { get; } = new();
        public List<LongMeasurement> JoinOutcomeMeasurements { get; } = new();

        public MetricCapture(SignalsMetrics metrics)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument, metrics.NlpExtractionDuration)
                    || ReferenceEquals(instrument, metrics.SignalsPreviewRequestsTotal)
                    || ReferenceEquals(instrument, metrics.SignalsSubmitRequestsTotal)
                    || ReferenceEquals(instrument, metrics.SignalJoinOutcomeTotal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (ReferenceEquals(instrument, metrics.NlpExtractionDuration))
                {
                    NlpDurationMeasurements.Add(new DoubleMeasurement(measurement, ToDict(tags)));
                }
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = ToDict(tags);
                if (ReferenceEquals(instrument, metrics.SignalsPreviewRequestsTotal))
                {
                    PreviewMeasurements.Add(new LongMeasurement(measurement, dict));
                }
                else if (ReferenceEquals(instrument, metrics.SignalsSubmitRequestsTotal))
                {
                    SubmitMeasurements.Add(new LongMeasurement(measurement, dict));
                }
                else if (ReferenceEquals(instrument, metrics.SignalJoinOutcomeTotal))
                {
                    JoinOutcomeMeasurements.Add(new LongMeasurement(measurement, dict));
                }
            });

            _listener.Start();
        }

        private static Dictionary<string, object?> ToDict(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value;
            }
            return dict;
        }

        public void Dispose() => _listener.Dispose();
    }

    // ── SignalsController.Preview — preview counter ──────────────────────────

    private static SignalsController CreateController(
        SignalsMetrics metrics,
        ISignalIngestionService ingestion,
        IAuthRepository? auth = null)
    {
        var controller = new SignalsController(
            ingestion,
            auth ?? Substitute.For<IAuthRepository>(),
            metrics);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static IConnectionMultiplexer BuildAllowAllRedis()
    {
        var mux = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        // StringIncrement returns 1 so the rate-limit gate never trips.
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(1L);
        db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>())
            .Returns(true);
        return mux;
    }

    private static IConnectionMultiplexer BuildRateLimitedRedis()
    {
        var mux = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(100L);
        return mux;
    }

    private static SignalPreviewRequestDto PreviewRequest(string freeText = "Pothole on Lusaka Road") =>
        new(freeText, null, null, null, null, null, null);

    [Fact]
    public async Task Preview_Success_IncrementsPreviewCounter_OutcomeSuccess()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.PreviewAsync(Arg.Any<SignalPreviewRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new SignalPreviewResponseDto("roads", "potholes", "difficult", 0.8,
                new SignalLocationDto(null, null, null, null, null, null, "road", 0.9, "nlp"),
                "temporary", "summary", ShouldSuggestJoin: false, RequiresLocationFallback: false));

        var controller = CreateController(scope.Metrics, ingestion);
        await controller.Preview(PreviewRequest(), CancellationToken.None, BuildAllowAllRedis());

        var m = Assert.Single(capture.PreviewMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(SignalsMetrics.OutcomeSuccess, m.Tags[SignalsMetrics.TagOutcome]);
        Assert.Empty(capture.SubmitMeasurements);
    }

    [Fact]
    public async Task Preview_ValidationMissingFreeText_IncrementsPreviewCounter_ValidationError()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Preview(PreviewRequest(""), CancellationToken.None, BuildAllowAllRedis()));

        var m = Assert.Single(capture.PreviewMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeValidationError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Preview_RateLimited_IncrementsPreviewCounter_ValidationError()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<RateLimitException>(() =>
            controller.Preview(PreviewRequest(), CancellationToken.None, BuildRateLimitedRedis()));

        var m = Assert.Single(capture.PreviewMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeValidationError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Preview_IngestionDependencyFails_IncrementsPreviewCounter_DependencyError()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.PreviewAsync(Arg.Any<SignalPreviewRequestDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DependencyException(ErrorCodes.DependencyNlpUnavailable, "NLP down"));
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<DependencyException>(() =>
            controller.Preview(PreviewRequest(), CancellationToken.None, BuildAllowAllRedis()));

        var m = Assert.Single(capture.PreviewMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeDependencyError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Preview_ClientCancelled_DoesNotIncrementPreviewCounter()
    {
        // OperationCanceledException is explicitly excluded from the outcome
        // taxonomy — disconnects should not bias any of the three buckets.
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.PreviewAsync(Arg.Any<SignalPreviewRequestDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.Preview(PreviewRequest(), CancellationToken.None, BuildAllowAllRedis()));

        Assert.Empty(capture.PreviewMeasurements);
    }

    // ── SignalsController.Submit — submit counter ────────────────────────────

    private static SignalSubmitRequestDto SubmitRequest(string idem = "idem-1", string deviceHash = "dev-1") =>
        new(idem, deviceHash, "Pothole", "roads", "potholes", "difficult",
            0.9, -1.3, 36.8, "Lusaka Rd", "road", 0.9, "nlp", "temporary", "summary", "en");

    [Fact]
    public async Task Submit_Success_IncrementsSubmitCounter_OutcomeSuccess()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.SubmitAsync(Arg.Any<SignalSubmitRequestDto>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new SignalSubmitResponseDto(
                Guid.NewGuid(), Guid.NewGuid(), true, "unconfirmed",
                Guid.NewGuid(), DateTime.UtcNow));
        var controller = CreateController(scope.Metrics, ingestion);

        await controller.Submit(SubmitRequest(), CancellationToken.None);

        var m = Assert.Single(capture.SubmitMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(SignalsMetrics.OutcomeSuccess, m.Tags[SignalsMetrics.TagOutcome]);
        Assert.Empty(capture.PreviewMeasurements);
    }

    [Fact]
    public async Task Submit_MissingIdempotencyKey_IncrementsSubmitCounter_ValidationError()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<ValidationException>(() =>
            controller.Submit(SubmitRequest(idem: ""), CancellationToken.None));

        var m = Assert.Single(capture.SubmitMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeValidationError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Submit_DuplicateIdempotency_IncrementsSubmitCounter_ValidationError()
    {
        // ConflictException on duplicate idempotency bucketizes as
        // validation_error per the controller's catch policy.
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.SubmitAsync(Arg.Any<SignalSubmitRequestDto>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(ErrorCodes.SignalDuplicate, "dup"));
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<ConflictException>(() =>
            controller.Submit(SubmitRequest(), CancellationToken.None));

        var m = Assert.Single(capture.SubmitMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeValidationError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Submit_SpatialDerivationFails_IncrementsSubmitCounter_DependencyError()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var ingestion = Substitute.For<ISignalIngestionService>();
        ingestion.SubmitAsync(Arg.Any<SignalSubmitRequestDto>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DependencyException(ErrorCodes.DependencySpatialDerivationFailed, "h3 failed"));
        var controller = CreateController(scope.Metrics, ingestion);

        await Assert.ThrowsAsync<DependencyException>(() =>
            controller.Submit(SubmitRequest(), CancellationToken.None));

        var m = Assert.Single(capture.SubmitMeasurements);
        Assert.Equal(SignalsMetrics.OutcomeDependencyError, m.Tags[SignalsMetrics.TagOutcome]);
    }

    // ── NLP histogram — AnthropicNlpExtractionService ────────────────────────

    private static IConfiguration MinimalNlpConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "test-key",
                ["Anthropic:Model"] = "claude-sonnet-4-6"
            })
            .Build();

    private static string ValidNlpResponseBody() =>
        """
        {
          "content": [
            { "text": "{\"category\":\"roads\",\"subcategory\":\"potholes\",\"condition_level\":\"difficult\",\"condition_confidence\":0.9,\"location\":{\"area_name\":\"Nairobi West\",\"location_confidence\":0.8,\"location_precision_type\":\"road\",\"location_source\":\"nlp\"},\"temporal_hint\":{\"type\":\"temporary\",\"confidence\":0.7},\"summary\":\"x\",\"should_suggest_join\":false}" }
          ]
        }
        """;

    [Fact]
    public async Task Nlp_Success_RecordsHistogramOnce_OutcomeSuccess()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidNlpResponseBody());
        using var http = new HttpClient(handler);
        var svc = new AnthropicNlpExtractionService(
            http,
            MinimalNlpConfig(),
            NullLogger<AnthropicNlpExtractionService>.Instance,
            scope.Metrics);

        var result = await svc.ExtractAsync(MakeNlpRequest());

        Assert.NotNull(result);
        var m = Assert.Single(capture.NlpDurationMeasurements);
        Assert.True(m.Value >= 0);
        Assert.Equal(SignalsMetrics.NlpOutcomeSuccess, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Nlp_Http500_RecordsHistogramOnce_OutcomeFallback()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        using var http = new HttpClient(handler);
        var svc = new AnthropicNlpExtractionService(
            http,
            MinimalNlpConfig(),
            NullLogger<AnthropicNlpExtractionService>.Instance,
            scope.Metrics);

        var result = await svc.ExtractAsync(MakeNlpRequest());

        Assert.Null(result);
        var m = Assert.Single(capture.NlpDurationMeasurements);
        Assert.Equal(SignalsMetrics.NlpOutcomeFallback, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Nlp_ParseError_RecordsHistogramOnce_OutcomeFallback()
    {
        // A 2xx with malformed content is the other fallback flavour — the
        // histogram still fires, tagged fallback (never silently success).
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "not-json");
        using var http = new HttpClient(handler);
        var svc = new AnthropicNlpExtractionService(
            http,
            MinimalNlpConfig(),
            NullLogger<AnthropicNlpExtractionService>.Instance,
            scope.Metrics);

        var result = await svc.ExtractAsync(MakeNlpRequest());

        Assert.Null(result);
        var m = Assert.Single(capture.NlpDurationMeasurements);
        Assert.Equal(SignalsMetrics.NlpOutcomeFallback, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Nlp_HttpClientTimeout_RecordsHistogramOnce_OutcomeTimeout()
    {
        // TaskCanceledException with an unsignaled caller CT is the HttpClient
        // timeout signal — the metric must distinguish it from other fallbacks
        // so operators can tell "upstream slow" from "upstream broken".
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException("timeout"));
        using var http = new HttpClient(handler);
        var svc = new AnthropicNlpExtractionService(
            http,
            MinimalNlpConfig(),
            NullLogger<AnthropicNlpExtractionService>.Instance,
            scope.Metrics);

        var result = await svc.ExtractAsync(MakeNlpRequest(), CancellationToken.None);

        Assert.Null(result);
        var m = Assert.Single(capture.NlpDurationMeasurements);
        Assert.Equal(SignalsMetrics.NlpOutcomeTimeout, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Nlp_CallerCancellation_DoesNotRecordHistogram()
    {
        // When the caller's CT is signaled before the call completes, the
        // cancellation is not a latency outcome — record nothing.
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new ThrowingHttpMessageHandler(new OperationCanceledException(cts.Token));
        using var http = new HttpClient(handler);
        var svc = new AnthropicNlpExtractionService(
            http,
            MinimalNlpConfig(),
            NullLogger<AnthropicNlpExtractionService>.Instance,
            scope.Metrics);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            svc.ExtractAsync(MakeNlpRequest(), cts.Token));

        Assert.Empty(capture.NlpDurationMeasurements);
    }

    private static NlpExtractionRequest MakeNlpRequest() =>
        new(FreeText: "Pothole on Lusaka Road",
            UserLatitude: null,
            UserLongitude: null,
            SelectedWard: null,
            Locale: null,
            KnownCity: null,
            CountryCode: null,
            CurrentTimeUtc: DateTime.UtcNow.ToString("o"),
            TaxonomyBlock: "roads: potholes");

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHttpMessageHandler(Exception ex) { _ex = ex; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(_ex);
    }

    // ── Join-outcome counter — ClusteringService ─────────────────────────────

    [Fact]
    public async Task Clustering_WhenCandidateJoined_EmitsJoinOutcome_JoinedExisting()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var (svc, repo, h3, civis) = BuildClustering(scope.Metrics);
        var signal = MakeSignal();
        var existingCluster = MakeCluster();
        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        var m = Assert.Single(capture.JoinOutcomeMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(SignalsMetrics.JoinOutcomeJoinedExisting, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Clustering_WhenNoCandidate_EmitsJoinOutcome_CreatedNew()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var (svc, repo, h3, civis) = BuildClustering(scope.Metrics);
        var signal = MakeSignal();
        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        var m = Assert.Single(capture.JoinOutcomeMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(SignalsMetrics.JoinOutcomeCreatedNew, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Clustering_WhenCandidateBelowThreshold_EmitsJoinOutcome_CreatedNew()
    {
        // Candidate exists but scores below 0.65 → falls through to the
        // create-new branch. The counter must reflect the decision
        // outcome, not the candidate-count intermediate signal.
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var (svc, repo, h3, civis) = BuildClustering(scope.Metrics);
        var signal = MakeSignal();
        // Different cell + stale + no matching condition → score well below 0.65
        var weakCandidate = MakeCluster(
            spatialCellId: "8928308281fffff", // different cell
            conditionSlug: null,
            lastSeenAgoHours: 100);
        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { weakCandidate });
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        var m = Assert.Single(capture.JoinOutcomeMeasurements);
        Assert.Equal(SignalsMetrics.JoinOutcomeCreatedNew, m.Tags[SignalsMetrics.TagOutcome]);
    }

    // ── Activation counter — CivisEvaluationService ──────────────────────────

    [Fact]
    public async Task Civis_WhenClusterActivates_EmitsJoinOutcome_ActivatedCluster()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var cluster = new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = CivicCategory.Roads,
            State = SignalState.Unconfirmed,
            RawConfirmationCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var repo = new FakeClusterRepo(cluster) { WrabCount = 3, ActiveMass = 3, UniqueDevices = 2 };
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(new CivisOptions
            {
                WrabRollingWindowDays = 30,
                JoinThreshold = 0.65,
                MinUniqueDevices = 2,
                DeactivationThreshold = 0.5,
                ActiveMassHorizonHours = 48,
                TimeScoreMaxAgeHours = 24.0,
                Roads = new CivisCategoryOptions
                {
                    BaseFloor = 2,
                    HalfLifeHours = 18.0,
                    MacfMin = 2,
                    MacfMax = 6
                }
            }),
            notificationQueue: null,
            logger: null,
            metrics: scope.Metrics);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
        var m = Assert.Single(capture.JoinOutcomeMeasurements);
        Assert.Equal(SignalsMetrics.JoinOutcomeActivatedCluster, m.Tags[SignalsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Civis_WhenClusterStaysUnconfirmed_NoActivationCounter()
    {
        using var scope = TestSignalsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var cluster = new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = CivicCategory.Roads,
            State = SignalState.Unconfirmed,
            // Insufficient raw count to trip MACF.
            RawConfirmationCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var repo = new FakeClusterRepo(cluster) { WrabCount = 3, ActiveMass = 1, UniqueDevices = 2 };
        var svc = new CivisEvaluationService(
            repo,
            Options.Create(new CivisOptions
            {
                WrabRollingWindowDays = 30,
                JoinThreshold = 0.65,
                MinUniqueDevices = 2,
                DeactivationThreshold = 0.5,
                ActiveMassHorizonHours = 48,
                TimeScoreMaxAgeHours = 24.0,
                Roads = new CivisCategoryOptions
                {
                    BaseFloor = 2,
                    HalfLifeHours = 18.0,
                    MacfMin = 2,
                    MacfMax = 6
                }
            }),
            notificationQueue: null,
            logger: null,
            metrics: scope.Metrics);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Unconfirmed, cluster.State);
        Assert.Empty(capture.JoinOutcomeMeasurements);
    }

    // ── Clustering test helpers (mirrors ClusteringServiceTests) ─────────────

    private static (ClusteringService svc, IClusterRepository repo, IH3CellService h3, ICivisEvaluationService civis)
        BuildClustering(SignalsMetrics metrics)
    {
        var repo = Substitute.For<IClusterRepository>();
        var h3 = Substitute.For<IH3CellService>();
        var civis = Substitute.For<ICivisEvaluationService>();
        var options = Options.Create(new CivisOptions
        {
            JoinThreshold = 0.65,
            TimeScoreMaxAgeHours = 24.0
        });
        var svc = new ClusteringService(repo, h3, civis, options, logger: null, metrics: metrics);
        return (svc, repo, h3, civis);
    }

    private static SignalEvent MakeSignal(string? spatialCellId = "8928308280fffff", CivicCategory category = CivicCategory.Water)
    {
        return new SignalEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Category = category,
            SubcategorySlug = "low-pressure",
            ConditionSlug = "no-water",
            NeutralSummary = "No water in the area.",
            TemporalType = "episodic",
            SpatialCellId = spatialCellId,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static SignalCluster MakeCluster(
        string spatialCellId = "8928308280fffff",
        CivicCategory category = CivicCategory.Water,
        string? conditionSlug = "no-water",
        double lastSeenAgoHours = 0.5)
    {
        return new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = category,
            State = SignalState.Unconfirmed,
            SpatialCellId = spatialCellId,
            DominantConditionSlug = conditionSlug,
            RawConfirmationCount = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            UpdatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            FirstSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            LastSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours)
        };
    }
}
