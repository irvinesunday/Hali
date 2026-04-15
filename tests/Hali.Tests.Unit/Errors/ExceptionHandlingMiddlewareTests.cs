using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Api.Middleware;
using Hali.Application.Errors;
using Hali.Tests.Unit.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hali.Tests.Unit.Errors;

public class ExceptionHandlingMiddlewareTests
{
    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(
            next,
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new ExceptionToApiErrorMapper(),
            TestMetrics.Create());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "test-trace-123";
        return context;
    }

    private static async Task<JsonDocument> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    [Fact]
    public async Task ClientDisconnect_DoesNotProduce500Envelope()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());
        var context = CreateContext();
        context.RequestAborted = cts.Token;

        await middleware.InvokeAsync(context);

        // Should NOT write a 500 error envelope for client disconnects
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task SuccessfulRequest_PassesThrough()
    {
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ValidationException_Returns400WithEnvelope()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ValidationException("Bad input", code: "validation.failed"));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var doc = await ReadResponseBody(context);
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("validation.failed", error.GetProperty("code").GetString());
        Assert.Equal("Bad input", error.GetProperty("message").GetString());
        Assert.Equal("test-trace-123", error.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task NotFoundException_Returns404WithEnvelope()
    {
        var middleware = CreateMiddleware(_ =>
            throw new NotFoundException("cluster.not_found", "Cluster not found."));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        Assert.Equal("cluster.not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ConflictException_Returns409WithEnvelope()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ConflictException("signal.duplicate", "Already submitted."));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(409, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        Assert.Equal("signal.duplicate", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task RateLimitException_Returns429WithEnvelope()
    {
        var middleware = CreateMiddleware(_ => throw new RateLimitException());
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(429, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        // H3 (#153): default RateLimitException code renamed from
        // `integrity.rate_limited` to `rate_limit.exceeded`.
        Assert.Equal(ErrorCodes.RateLimitExceeded, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DependencyException_Returns503WithEnvelope()
    {
        var middleware = CreateMiddleware(_ =>
            throw new DependencyException("dependency.nlp_unavailable", "NLP down."));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(503, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        Assert.Equal("dependency.nlp_unavailable", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnhandledException_Returns500WithSafeEnvelope()
    {
        var middleware = CreateMiddleware(_ =>
            throw new Exception("Internal DB connection: Host=secret.db;Password=abc123"));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("server.internal_error", error.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", error.GetProperty("message").GetString());
        Assert.Equal("test-trace-123", error.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task UnhandledException_DoesNotLeakStackTrace()
    {
        var middleware = CreateMiddleware(_ =>
            throw new NullReferenceException("Object reference not set"));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("NullReferenceException", body);
        Assert.DoesNotContain("Object reference not set", body);
        Assert.DoesNotContain("at Hali", body);
        Assert.DoesNotContain("StackTrace", body);
    }

    [Fact]
    public async Task UnauthorizedException_Returns401WithEnvelope()
    {
        var middleware = CreateMiddleware(_ => throw new UnauthorizedException());
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        var doc = await ReadResponseBody(context);
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("auth.unauthorized", error.GetProperty("code").GetString());
        Assert.Equal("test-trace-123", error.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task AllResponses_IncludeTraceId()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ValidationException("test"));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        var doc = await ReadResponseBody(context);
        Assert.True(doc.RootElement.GetProperty("error").TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task ValidationWithFieldErrors_IncludesDetailsInEnvelope()
    {
        var fields = new System.Collections.Generic.Dictionary<string, string[]>
        {
            ["latitude"] = ["Must be between -90 and 90."]
        };
        var middleware = CreateMiddleware(_ =>
            throw new ValidationException("Validation failed", fieldErrors: fields));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        var doc = await ReadResponseBody(context);
        var error = doc.RootElement.GetProperty("error");
        Assert.True(error.TryGetProperty("details", out var details));
        Assert.True(details.TryGetProperty("fields", out _));
    }
}
