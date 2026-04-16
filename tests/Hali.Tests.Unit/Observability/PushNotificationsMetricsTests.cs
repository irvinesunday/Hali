using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Application.Auth;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Contracts.Notifications;
using Hali.Domain.Entities.Auth;
using Hali.Infrastructure.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Verifies that the push-notification surface emits the three instruments
/// owned by <see cref="PushNotificationsMetrics"/>:
/// <list type="bullet">
///   <item><description><c>push_send_attempts_total</c> — one increment per
///     <see cref="PushMessage"/>, tagged with the per-message outcome
///     derived from the Expo response or from the batch-level HTTP / transport
///     disposition;</description></item>
///   <item><description><c>push_send_duration_seconds</c> — one measurement
///     per Expo send call, tagged with the batch-level outcome;</description></item>
///   <item><description><c>push_token_registrations_total</c> — one
///     increment per successful <c>POST /v1/devices/push-token</c> call,
///     tagged with <c>result</c> (<c>new</c>, <c>updated</c>, or
///     <c>unchanged</c>).</description></item>
/// </list>
///
/// Each test owns an isolated <see cref="PushNotificationsMetrics"/> via
/// <see cref="TestPushNotificationsMetrics"/> so the <see cref="MeterListener"/>
/// only observes measurements from that test's meter — keeping the suite
/// parallel-safe.
/// </summary>
public class PushNotificationsMetricsTests
{
    private sealed record DoubleMeasurement(double Value, Dictionary<string, object?> Tags);
    private sealed record LongMeasurement(long Value, Dictionary<string, object?> Tags);

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<LongMeasurement> AttemptMeasurements { get; } = new();
        public List<DoubleMeasurement> DurationMeasurements { get; } = new();
        public List<LongMeasurement> RegistrationMeasurements { get; } = new();

        public MetricCapture(PushNotificationsMetrics metrics)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument, metrics.PushSendAttemptsTotal)
                    || ReferenceEquals(instrument, metrics.PushSendDuration)
                    || ReferenceEquals(instrument, metrics.PushTokenRegistrationsTotal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = ToDict(tags);
                if (ReferenceEquals(instrument, metrics.PushSendAttemptsTotal))
                {
                    AttemptMeasurements.Add(new LongMeasurement(measurement, dict));
                }
                else if (ReferenceEquals(instrument, metrics.PushTokenRegistrationsTotal))
                {
                    RegistrationMeasurements.Add(new LongMeasurement(measurement, dict));
                }
            });

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (ReferenceEquals(instrument, metrics.PushSendDuration))
                {
                    DurationMeasurements.Add(new DoubleMeasurement(measurement, ToDict(tags)));
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

    // ── ExpoPushNotificationService — counter + histogram ────────────────────

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHttpMessageHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHttpMessageHandler(Exception ex) { _ex = ex; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(_ex);
    }

    private static ExpoPushNotificationService BuildService(
        HttpMessageHandler handler,
        PushNotificationsMetrics metrics)
    {
        var http = new HttpClient(handler);
        return new ExpoPushNotificationService(
            http,
            NullLogger<ExpoPushNotificationService>.Instance,
            metrics);
    }

    private static List<PushMessage> MakeBatch(int count)
    {
        var list = new List<PushMessage>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(new PushMessage(
                Token: $"ExponentPushToken[xxxxxxxxxxxx-{i}]",
                Title: "Title",
                Body: "Body",
                Data: null));
        }
        return list;
    }

    [Fact]
    public async Task Send_AllTicketsOk_EmitsOneSuccessPerMessageAndSuccessHistogram()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        string body = """{"data":[{"status":"ok"},{"status":"ok"},{"status":"ok"}]}""";
        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.OK, body), scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(3));

        // 3 success measurements (one per ticket); histogram fires once.
        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(3L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomeSuccess, attempt.Tags[PushNotificationsMetrics.TagOutcome]);

        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.True(duration.Value >= 0);
        Assert.Equal(PushNotificationsMetrics.OutcomeSuccess, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_MixedTickets_EmitsOneBucketPerDistinctOutcome()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        // Three messages with four distinct dispositions across them is the
        // worst-case cardinality per response — asserts the classifier maps
        // each bucket correctly and does not double-count.
        string body = """
        {
          "data": [
            { "status": "ok" },
            { "status": "error", "details": { "error": "DeviceNotRegistered" } },
            { "status": "error", "details": { "error": "MessageTooBig" } },
            { "status": "error", "details": { "error": "MessageRateExceeded" } },
            { "status": "error", "details": { "error": "InvalidCredentials" } }
          ]
        }
        """;
        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.OK, body), scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(5));

        var byOutcome = new Dictionary<string, long>();
        foreach (var m in capture.AttemptMeasurements)
        {
            var outcome = (string)m.Tags[PushNotificationsMetrics.TagOutcome]!;
            byOutcome[outcome] = byOutcome.GetValueOrDefault(outcome) + m.Value;
        }

        Assert.Equal(1L, byOutcome[PushNotificationsMetrics.OutcomeSuccess]);
        Assert.Equal(1L, byOutcome[PushNotificationsMetrics.OutcomeDeviceInvalid]);
        // MessageTooBig + InvalidCredentials both map to permanent_error.
        Assert.Equal(2L, byOutcome[PushNotificationsMetrics.OutcomePermanentError]);
        // MessageRateExceeded maps to transient_error.
        Assert.Equal(1L, byOutcome[PushNotificationsMetrics.OutcomeTransientError]);

        // Histogram fires once with the batch-level success outcome because
        // the HTTP call itself succeeded (2xx). device_invalid /
        // permanent_error dispositions are per-message only.
        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.Equal(PushNotificationsMetrics.OutcomeSuccess, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_UnknownErrorCode_DefaultsToTransient()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        // Unrecognised Expo error code must never slip into success/permanent
        // — transient is the safe retriable default.
        string body = """{"data":[{"status":"error","details":{"error":"SomeFutureErrorCodeExpoAdds"}}]}""";
        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.OK, body), scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(1));

        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(1L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomeTransientError, attempt.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_Http5xx_EmitsTransientErrorForEveryMessageAndHistogram()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.BadGateway, "{}"), scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(2));

        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(2L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomeTransientError, attempt.Tags[PushNotificationsMetrics.TagOutcome]);

        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.Equal(PushNotificationsMetrics.OutcomeTransientError, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_Http4xx_EmitsPermanentErrorForEveryMessageAndHistogram()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        // 401 is the canonical auth failure for Expo batches — must never
        // be retried (would hammer Expo with bad credentials).
        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "{}"), scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(4));

        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(4L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomePermanentError, attempt.Tags[PushNotificationsMetrics.TagOutcome]);

        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.Equal(PushNotificationsMetrics.OutcomePermanentError, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_TransportException_EmitsTransientErrorAndStillRethrows()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var svc = BuildService(
            new ThrowingHttpMessageHandler(new HttpRequestException("dns failure")),
            scope.Metrics);

        await Assert.ThrowsAsync<HttpRequestException>(() => svc.SendBatchAsync(MakeBatch(2)));

        // Metrics must still record even though the exception propagates.
        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(2L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomeTransientError, attempt.Tags[PushNotificationsMetrics.TagOutcome]);

        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.Equal(PushNotificationsMetrics.OutcomeTransientError, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    [Fact]
    public async Task Send_CallerCancellation_DoesNotEmitMetrics()
    {
        // Mirrors SignalsMetrics / ClustersMetrics: a caller-signaled
        // cancellation is not a send outcome — no counter or histogram
        // measurement is recorded.
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var svc = BuildService(
            new ThrowingHttpMessageHandler(new OperationCanceledException(cts.Token)),
            scope.Metrics);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            svc.SendBatchAsync(MakeBatch(1), cts.Token));

        Assert.Empty(capture.AttemptMeasurements);
        Assert.Empty(capture.DurationMeasurements);
    }

    [Fact]
    public async Task Send_EmptyBatch_EmitsNothing()
    {
        // Zero work = zero metrics. Guards against an off-by-one that
        // would quietly inflate the counter on idle worker passes.
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var svc = BuildService(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"), scope.Metrics);

        await svc.SendBatchAsync(Array.Empty<PushMessage>());

        Assert.Empty(capture.AttemptMeasurements);
        Assert.Empty(capture.DurationMeasurements);
    }

    [Fact]
    public async Task Send_UnparseableResponseBody_FallsBackToSuccessPerRequestMessage()
    {
        // A 2xx with malformed body should not silently lose attempts —
        // counter falls back to one success per request-side message.
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var svc = BuildService(
            new StubHttpMessageHandler(HttpStatusCode.OK, "not-json"),
            scope.Metrics);

        await svc.SendBatchAsync(MakeBatch(3));

        var attempt = Assert.Single(capture.AttemptMeasurements);
        Assert.Equal(3L, attempt.Value);
        Assert.Equal(PushNotificationsMetrics.OutcomeSuccess, attempt.Tags[PushNotificationsMetrics.TagOutcome]);

        var duration = Assert.Single(capture.DurationMeasurements);
        Assert.Equal(PushNotificationsMetrics.OutcomeSuccess, duration.Tags[PushNotificationsMetrics.TagOutcome]);
    }

    // ── DevicesController.RegisterPushToken — registration counter ───────────

    private static DevicesController CreateDevicesController(
        PushNotificationsMetrics metrics,
        IAuthRepository auth)
    {
        var controller = new DevicesController(
            auth,
            NullLogger<DevicesController>.Instance,
            metrics);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task RegisterPushToken_NewToken_EmitsResultNew()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceFingerprintHash = "hash",
            ExpoPushToken = null,
        };
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync("hash", Arg.Any<CancellationToken>()).Returns(device);

        var controller = CreateDevicesController(scope.Metrics, auth);
        var dto = new RegisterPushTokenRequestDto { ExpoPushToken = "ExponentPushToken[aaa]", DeviceHash = "hash" };

        await controller.RegisterPushToken(dto, CancellationToken.None);

        var m = Assert.Single(capture.RegistrationMeasurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(PushNotificationsMetrics.ResultNew, m.Tags[PushNotificationsMetrics.TagResult]);
    }

    [Fact]
    public async Task RegisterPushToken_DifferentToken_EmitsResultUpdated()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceFingerprintHash = "hash",
            ExpoPushToken = "ExponentPushToken[old]",
        };
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync("hash", Arg.Any<CancellationToken>()).Returns(device);

        var controller = CreateDevicesController(scope.Metrics, auth);
        var dto = new RegisterPushTokenRequestDto { ExpoPushToken = "ExponentPushToken[new]", DeviceHash = "hash" };

        await controller.RegisterPushToken(dto, CancellationToken.None);

        var m = Assert.Single(capture.RegistrationMeasurements);
        Assert.Equal(PushNotificationsMetrics.ResultUpdated, m.Tags[PushNotificationsMetrics.TagResult]);
    }

    [Fact]
    public async Task RegisterPushToken_SameToken_EmitsResultUnchanged()
    {
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        // Exact ordinal match — the comparison must be case-sensitive to
        // avoid collapsing two legitimately different Expo tokens onto the
        // same "unchanged" bucket.
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceFingerprintHash = "hash",
            ExpoPushToken = "ExponentPushToken[same]",
        };
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync("hash", Arg.Any<CancellationToken>()).Returns(device);

        var controller = CreateDevicesController(scope.Metrics, auth);
        var dto = new RegisterPushTokenRequestDto { ExpoPushToken = "ExponentPushToken[same]", DeviceHash = "hash" };

        await controller.RegisterPushToken(dto, CancellationToken.None);

        var m = Assert.Single(capture.RegistrationMeasurements);
        Assert.Equal(PushNotificationsMetrics.ResultUnchanged, m.Tags[PushNotificationsMetrics.TagResult]);
    }

    [Fact]
    public async Task RegisterPushToken_CaseDifferenceCountsAsUpdated()
    {
        // Expo tokens are opaque case-sensitive strings. A case-only diff
        // must not be collapsed onto "unchanged".
        using var scope = TestPushNotificationsMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceFingerprintHash = "hash",
            ExpoPushToken = "ExponentPushToken[ABC]",
        };
        var auth = Substitute.For<IAuthRepository>();
        auth.FindDeviceByFingerprintAsync("hash", Arg.Any<CancellationToken>()).Returns(device);

        var controller = CreateDevicesController(scope.Metrics, auth);
        var dto = new RegisterPushTokenRequestDto { ExpoPushToken = "ExponentPushToken[abc]", DeviceHash = "hash" };

        await controller.RegisterPushToken(dto, CancellationToken.None);

        var m = Assert.Single(capture.RegistrationMeasurements);
        Assert.Equal(PushNotificationsMetrics.ResultUpdated, m.Tags[PushNotificationsMetrics.TagResult]);
    }
}
