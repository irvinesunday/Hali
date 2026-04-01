using Hali.Application.Clusters;

namespace Hali.Tests.Unit.Clusters;

/// <summary>
/// Pure formula tests for CivisCalculator.
/// No DB, no mocks — formulas only.
/// </summary>
public class CivisCalculatorTests
{
    // -------------------------------------------------------------------------
    // MACF = clamp(ceil(base_floor + log2(SDS + 1)), macf_min, macf_max)
    //
    // Verified expected values:
    //   SDS=0: ceil(2 + log2(1))   = ceil(2 + 0)     = 2
    //   SDS=1: ceil(2 + log2(2))   = ceil(2 + 1)     = 3
    //   SDS=5: ceil(2 + log2(6))   = ceil(2 + 2.585) = ceil(4.585) = 5
    //
    // Category-specific params from mvp_locked_decisions.md §9.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("roads",          2, 2, 6, 0.0, 2)]
    [InlineData("roads",          2, 2, 6, 1.0, 3)]
    [InlineData("roads",          2, 2, 6, 5.0, 5)]
    [InlineData("transport",      2, 2, 5, 0.0, 2)]
    [InlineData("transport",      2, 2, 5, 1.0, 3)]
    [InlineData("transport",      2, 2, 5, 5.0, 5)]  // ceil(4.585)=5, clamp to max=5 → 5
    [InlineData("electricity",    2, 2, 6, 0.0, 2)]
    [InlineData("electricity",    2, 2, 6, 1.0, 3)]
    [InlineData("electricity",    2, 2, 6, 5.0, 5)]
    [InlineData("water",          2, 2, 7, 0.0, 2)]
    [InlineData("water",          2, 2, 7, 1.0, 3)]
    [InlineData("water",          2, 2, 7, 5.0, 5)]
    [InlineData("environment",    2, 2, 6, 0.0, 2)]
    [InlineData("environment",    2, 2, 6, 1.0, 3)]
    [InlineData("environment",    2, 2, 6, 5.0, 5)]
    [InlineData("safety",         2, 2, 6, 0.0, 2)]
    [InlineData("safety",         2, 2, 6, 1.0, 3)]
    [InlineData("safety",         2, 2, 6, 5.0, 5)]
    [InlineData("infrastructure", 2, 2, 6, 0.0, 2)]
    [InlineData("infrastructure", 2, 2, 6, 1.0, 3)]
    [InlineData("infrastructure", 2, 2, 6, 5.0, 5)]
    public void ComputeMacf_VariousSds_ReturnsExpected(
        string categoryName, int baseFloor, int macfMin, int macfMax, double sds, int expected)
    {
        var opts = new CivisCategoryOptions { BaseFloor = baseFloor, MacfMin = macfMin, MacfMax = macfMax };
        var result = CivisCalculator.ComputeMacf(sds, opts);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeMacf_SdsVeryHigh_ClampsToMacfMax()
    {
        // Roads macf_max=6: SDS=100 → ceil(2 + log2(101)) ≈ ceil(8.66) = 9, clamped to 6
        var opts = new CivisCategoryOptions { BaseFloor = 2, MacfMin = 2, MacfMax = 6 };
        Assert.Equal(6, CivisCalculator.ComputeMacf(100.0, opts));
    }

    [Fact]
    public void ComputeMacf_SdsZero_ReturnsBaseFloor()
    {
        // SDS=0 → MACF = macf_min (since ceil(2 + 0) = 2 = base_floor = macf_min)
        var opts = new CivisCategoryOptions { BaseFloor = 2, MacfMin = 2, MacfMax = 6 };
        Assert.Equal(2, CivisCalculator.ComputeMacf(0.0, opts));
    }

    // -------------------------------------------------------------------------
    // SDS = active_mass / max(wrab, base_floor)
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputeSds_WrabBelowBaseFloor_UsesBaseFloor()
    {
        // effective_WRAB = max(0, 2) = 2; SDS = 4 / 2 = 2.0
        var sds = CivisCalculator.ComputeSds(activeMass: 4, wrab: 0, baseFloor: 2);
        Assert.Equal(2.0, sds, precision: 6);
    }

    [Fact]
    public void ComputeSds_WrabAboveBaseFloor_UsesWrab()
    {
        // effective_WRAB = max(10, 2) = 10; SDS = 5 / 10 = 0.5
        var sds = CivisCalculator.ComputeSds(activeMass: 5, wrab: 10, baseFloor: 2);
        Assert.Equal(0.5, sds, precision: 6);
    }

    // -------------------------------------------------------------------------
    // Decay: live_mass = initial * exp(-lambda * elapsed_hours)
    // Lambda = ln(2) / half_life_hours
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(18)]   // roads / safety
    [InlineData(8)]    // transport
    [InlineData(12)]   // electricity
    [InlineData(24)]   // water / infrastructure
    [InlineData(36)]   // environment
    public void ApplyDecay_AtT0_ReturnsInitialMass(double halfLifeHours)
    {
        var lambda = CivisCalculator.ComputeLambda(halfLifeHours);
        var result = CivisCalculator.ApplyDecay(initialMass: 10.0, lambda: lambda, elapsedHours: 0);
        Assert.Equal(10.0, result, precision: 6);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(36)]
    public void ApplyDecay_AtHalfLife_ReturnsHalfOfInitial(double halfLifeHours)
    {
        var lambda = CivisCalculator.ComputeLambda(halfLifeHours);
        var result = CivisCalculator.ApplyDecay(initialMass: 10.0, lambda: lambda, elapsedHours: halfLifeHours);
        Assert.Equal(5.0, result, precision: 4);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(36)]
    public void ApplyDecay_AtTwoHalfLives_ReturnsQuarterOfInitial(double halfLifeHours)
    {
        var lambda = CivisCalculator.ComputeLambda(halfLifeHours);
        var result = CivisCalculator.ApplyDecay(initialMass: 10.0, lambda: lambda, elapsedHours: halfLifeHours * 2);
        Assert.Equal(2.5, result, precision: 4);
    }

    [Fact]
    public void ComputeLambda_HalfLifeOf24Hours_CorrectLambda()
    {
        // lambda = ln(2)/24 ≈ 0.02888
        var lambda = CivisCalculator.ComputeLambda(24);
        Assert.Equal(Math.Log(2) / 24, lambda, precision: 10);
    }
}
