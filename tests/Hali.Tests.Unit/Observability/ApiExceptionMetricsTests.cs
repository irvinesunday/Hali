using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Api.Middleware;
using Hali.Api.Observability;
using Hali.Application.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Verifies that <c>ExceptionHandlingMiddleware</c> emits the
/// <c>api_exceptions_total</c> counter with the intended wire-visible tags,
/// and that non-exception paths do not emit the metric.
///
/// Each test constructs an isolated <see cref="ApiMetrics"/> instance so the
/// <see cref="MeterListener"/> only observes measurements from that test's
/// meter. This keeps tests parallel-safe.
/// </summary>
public class ApiExceptionMetricsTests
{
    private sealed record Measurement(long Value, Dictionary<string, object?> Tags);

    /// <summary>
    /// Captures every <c>api_exceptions_total</c> measurement emitted by the
    /// supplied <see cref="ApiMetrics"/> instance for the lifetime of the
    /// listener.
    /// </summary>
    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<Measurement> Measurements { get; } = new();

        public MetricCapture(ApiMetrics metrics)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument, metrics.ApiExceptionsTotal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            {
                var dict = new Dictionary<string, object?>(tags.Length);
                foreach (var tag in tags)
                {
                    dict[tag.Key] = tag.Value;
                }
                Measurements.Add(new Measurement(measurement, dict));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next, ApiMetrics metrics)
        => new(next, NullLogger<ExceptionHandlingMiddleware>.Instance, new ExceptionToApiErrorMapper(), metrics);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "trace-abc";
        return context;
    }

    [Fact]
    public async Task ValidationException_EmitsCounter_WithMappedCodeAndValidationCategory()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new ValidationException("Bad input", code: ErrorCodes.ValidationMissingField),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(1L, m.Value);
        Assert.Equal(ErrorCodes.ValidationMissingField, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("validation", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(400, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task NotFoundException_EmitsCounter_With404()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new NotFoundException(ErrorCodes.ClusterNotFound, "Cluster not found."),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(ErrorCodes.ClusterNotFound, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("notfound", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(404, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task RateLimitException_EmitsCounter_With429()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(_ => throw new RateLimitException(), scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(ErrorCodes.RateLimitExceeded, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("ratelimit", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(429, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task DependencyException_EmitsCounter_With503()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new DependencyException(ErrorCodes.DependencyNlpUnavailable, "NLP down."),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(ErrorCodes.DependencyNlpUnavailable, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("dependency", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(503, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task UnmappedException_EmitsCounter_TaggedAsRedactedServerError()
    {
        // A bare Exception never surfaces its internal details on the wire —
        // the mapper returns ServerInternalError with ErrorCategory.Unexpected.
        // The metric must reflect the redacted wire view, not the raw
        // exception type, so dashboards count what users actually observed.
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new Exception("Internal DB password leak: Host=secret.db;Password=abc"),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(ErrorCodes.ServerInternalError, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("unexpected", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(500, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task InvariantViolation_EmitsCounter_TaggedAsRedactedServerError_NotInternalCode()
    {
        // InvariantViolationException carries an internal-only code
        // (e.g. "clustering.no_spatial_cell") for log/trace debuggability.
        // The mapper redacts it to ServerInternalError on the wire; the
        // metric MUST bucket with the wire outcome so alerts/dashboards stay
        // truthful — an internal-only code never appears as a metric value.
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new InvariantViolationException(
                ErrorCodes.ClusteringNoSpatialCell,
                "Signal reached clustering with no spatial cell."),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        var m = Assert.Single(capture.Measurements);
        Assert.Equal(ErrorCodes.ServerInternalError, m.Tags[ApiMetrics.TagErrorCode]);
        Assert.Equal("unexpected", m.Tags[ApiMetrics.TagErrorCategory]);
        Assert.Equal(500, m.Tags[ApiMetrics.TagStatusCode]);
    }

    [Fact]
    public async Task SuccessfulRequest_DoesNotEmitCounter()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, scope.Metrics);

        await middleware.InvokeAsync(CreateContext());

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public async Task ClientDisconnectCancellation_DoesNotEmitCounter()
    {
        // Client disconnects surface as OperationCanceledException when
        // context.RequestAborted is signalled. That path is intentionally
        // not a handled API exception — it must not pollute error metrics.
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = CreateContext();
        context.RequestAborted = cts.Token;

        var middleware = CreateMiddleware(_ => throw new OperationCanceledException(), scope.Metrics);

        await middleware.InvokeAsync(context);

        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public async Task TwoExceptions_EmitTwoMeasurements()
    {
        using var scope = TestMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);

        var middleware = CreateMiddleware(
            _ => throw new ValidationException("bad", code: ErrorCodes.ValidationFailed),
            scope.Metrics);

        await middleware.InvokeAsync(CreateContext());
        await middleware.InvokeAsync(CreateContext());

        Assert.Equal(2, capture.Measurements.Count);
        Assert.All(capture.Measurements, m => Assert.Equal(1L, m.Value));
    }
}
