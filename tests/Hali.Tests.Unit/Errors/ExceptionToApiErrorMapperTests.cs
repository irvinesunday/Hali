using System;
using System.Collections.Generic;
using Hali.Api.Errors;
using Hali.Application.Errors;
using Hali.Domain.Errors;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hali.Tests.Unit.Errors;

public class ExceptionToApiErrorMapperTests
{
    private readonly ExceptionToApiErrorMapper _mapper = new();

    [Fact]
    public void Map_ValidationException_Returns400()
    {
        var ex = new ValidationException("Bad input", code: "validation.failed");
        var result = _mapper.Map(ex);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("validation.failed", result.Code);
        Assert.Equal("Bad input", result.Message);
        Assert.Equal(LogLevel.Information, result.LogLevel);
    }

    [Fact]
    public void Map_ValidationException_WithFieldErrors_IncludesDetails()
    {
        var fields = new Dictionary<string, string[]>
        {
            ["latitude"] = ["Latitude must be between -90 and 90."]
        };
        var ex = new ValidationException("Validation failed", fieldErrors: fields);
        var result = _mapper.Map(ex);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.Details);
    }

    [Fact]
    public void Map_NotFoundException_Returns404()
    {
        var ex = new NotFoundException("cluster.not_found", "Cluster not found.");
        var result = _mapper.Map(ex);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("cluster.not_found", result.Code);
        Assert.Equal(LogLevel.Information, result.LogLevel);
    }

    [Fact]
    public void Map_ConflictException_Returns409()
    {
        var ex = new ConflictException("signal.duplicate", "Signal already submitted.");
        var result = _mapper.Map(ex);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("signal.duplicate", result.Code);
        Assert.Equal(LogLevel.Information, result.LogLevel);
    }

    [Fact]
    public void Map_RateLimitException_Returns429()
    {
        var ex = new RateLimitException();
        var result = _mapper.Map(ex);
        Assert.Equal(429, result.StatusCode);
        // H3 (#153): default RateLimitException code renamed from
        // `integrity.rate_limited` to `rate_limit.exceeded`.
        Assert.Equal(ErrorCodes.RateLimitExceeded, result.Code);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
    }

    [Fact]
    public void Map_DependencyException_Returns503()
    {
        var ex = new DependencyException("dependency.nlp_unavailable", "NLP unavailable.");
        var result = _mapper.Map(ex);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("dependency.nlp_unavailable", result.Code);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
    }

    [Fact]
    public void Map_UnknownException_Returns500WithSafeMessage()
    {
        var ex = new NullReferenceException("Object reference not set");
        var result = _mapper.Map(ex);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("server.internal_error", result.Code);
        Assert.Equal("An unexpected error occurred.", result.Message);
        Assert.Equal(LogLevel.Error, result.LogLevel);
    }

    [Fact]
    public void Map_UnknownException_DoesNotLeakInternalMessage()
    {
        var ex = new Exception("SELECT * FROM accounts WHERE id = 'injection'; DROP TABLE accounts;");
        var result = _mapper.Map(ex);
        Assert.Equal(500, result.StatusCode);
        Assert.DoesNotContain("SELECT", result.Message);
        Assert.DoesNotContain("DROP", result.Message);
        Assert.Equal("An unexpected error occurred.", result.Message);
    }

    [Fact]
    public void Map_DbException_DoesNotLeakConnectionString()
    {
        var ex = new InvalidOperationException("Host=db.internal;Port=5432;Database=hali;Password=secret123");
        var result = _mapper.Map(ex);
        Assert.Equal(500, result.StatusCode);
        Assert.DoesNotContain("secret", result.Message);
        Assert.DoesNotContain("Host=", result.Message);
    }

    [Fact]
    public void Map_UnauthorizedException_Returns401()
    {
        var ex = new UnauthorizedException();
        var result = _mapper.Map(ex);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("auth.unauthorized", result.Code);
        Assert.Equal(LogLevel.Warning, result.LogLevel);
    }

    [Fact]
    public void Map_InvariantViolationException_RedactsInternalCodeOnTheWire()
    {
        // H3 (#153): typed AppException carrying ErrorCategory.Unexpected
        // (internal invariant violation) MUST be redacted on the wire to
        // server.internal_error — the typed Code is a log/trace identifier
        // only and must never leak the internal invariant name (e.g.
        // clustering.no_spatial_cell) to clients. This is security-sensitive
        // and the catch-all unknown-exception path does the same thing
        // (Map_UnknownException_Returns500WithSafeMessage above); this test
        // pins the typed-exception path specifically so regressions here
        // can't be masked by that one.
        var ex = new InvariantViolationException(
            ErrorCodes.ClusteringNoSpatialCell,
            "Signal reached clustering without spatial cell.");

        var result = _mapper.Map(ex);

        Assert.Equal(500, result.StatusCode);
        Assert.Equal(ErrorCodes.ServerInternalError, result.Code);
        Assert.NotEqual(ErrorCodes.ClusteringNoSpatialCell, result.Code);
        Assert.Equal("An unexpected error occurred.", result.Message);
        Assert.Equal(LogLevel.Error, result.LogLevel);
    }
}
