using System;
using Hali.Application.Institutions;
using Xunit;

namespace Hali.Tests.Unit.Institutions;

/// <summary>
/// Unit coverage for the pure-logic helpers on
/// <see cref="InstitutionReadService"/>. The service itself is
/// integration-covered; these tests pin the deterministic mappings +
/// the opaque cursor codec so regressions are caught without spinning
/// up the full WebApplicationFactory.
/// </summary>
public sealed class InstitutionReadServiceLogicTests
{
    [Theory]
    [InlineData(0, "calm")]
    [InlineData(1, "elevated")]
    [InlineData(2, "elevated")]
    [InlineData(3, "active")]
    [InlineData(10, "active")]
    public void ClassifyCondition_MapsActiveCountToCondition(int activeSignals, string expected)
    {
        Assert.Equal(expected, InstitutionReadService.ClassifyCondition(activeSignals));
    }

    [Theory]
    [InlineData("active", 0, "active")]
    [InlineData("possible_restoration", 99, "elevated")]
    [InlineData("unconfirmed", 1, "elevated")]
    [InlineData("resolved", 0, "calm")]
    public void DeriveCondition_StateAndAffectedDriveCondition(
        string state, int affected, string expected)
    {
        Assert.Equal(expected, InstitutionReadService.DeriveCondition(state, affected));
    }

    [Theory]
    [InlineData("possible_restoration", 0, "possible_restoration")]
    [InlineData("active", 0, "slowing")]
    [InlineData("active", 3, "stable")]
    [InlineData("active", 7, "growing")]
    public void DeriveTrend_StateAndReportCountDriveTrend(
        string state, int reports, string expected)
    {
        Assert.Equal(expected, InstitutionReadService.DeriveTrend(state, reports));
    }

    [Theory]
    [InlineData(-5, 20)]
    [InlineData(0, 20)]
    [InlineData(5, 5)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void NormaliseLimit_ClampsToBoundedRange(int requested, int expected)
    {
        Assert.Equal(expected, InstitutionReadService.NormaliseLimit(requested));
    }

    [Fact]
    public void EncodeCursor_RoundTrips()
    {
        var ts = new DateTime(2026, 4, 17, 12, 30, 45, DateTimeKind.Utc);
        var id = Guid.NewGuid();

        var encoded = InstitutionReadService.EncodeCursor(ts, id);
        var (decodedTs, decodedId) = InstitutionReadService.DecodeCursor(encoded);

        Assert.Equal(ts, decodedTs);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-cursor")]
    [InlineData("!!!not-base64!!!")]
    public void DecodeCursor_InvalidInput_ReturnsNulls(string cursor)
    {
        var (ts, id) = InstitutionReadService.DecodeCursor(cursor);
        Assert.Null(ts);
        Assert.Null(id);
    }

    [Fact]
    public void ValidateStateFilter_NullOrEmpty_IsAccepted()
    {
        // Both null and empty are "no filter applied" — no exception expected.
        InstitutionReadService.ValidateStateFilter(null);
        InstitutionReadService.ValidateStateFilter(string.Empty);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("growing")]
    [InlineData("needs_attention")]
    [InlineData("restoration")]
    public void ValidateStateFilter_CanonicalValue_IsAccepted(string state)
    {
        InstitutionReadService.ValidateStateFilter(state);
    }

    [Theory]
    [InlineData("resolved")]
    [InlineData("calm")]
    [InlineData("unknown")]
    public void ValidateStateFilter_NonCanonical_ThrowsValidation(string state)
    {
        Assert.Throws<Hali.Application.Errors.ValidationException>(
            () => InstitutionReadService.ValidateStateFilter(state));
    }

    [Theory]
    [InlineData("PossibleRestoration", "possible_restoration")]
    [InlineData("Active", "active")]
    [InlineData("Unconfirmed", "unconfirmed")]
    public void ToSnakeCase_PascalCaseEnum_ConvertsCorrectly(string pascal, string expected)
    {
        // Guards against the ToLowerInvariant trap in COPILOT_LESSONS.md §5
        // — "possiblerestoration" instead of "possible_restoration".
        Assert.Equal(expected, InstitutionReadService.ToSnakeCase(pascal));
    }

    [Fact]
    public void ComputeTimeActiveSeconds_FutureTimestamp_ReturnsZero()
    {
        var future = DateTime.UtcNow.AddHours(1);
        Assert.Equal(0L, InstitutionReadService.ComputeTimeActiveSeconds(future));
    }
}
