using System;
using Hali.Application.Clusters;
using Xunit;

namespace Hali.Tests.Unit.Clusters;

public class CivisCalculatorTests
{
	[Theory]
	[InlineData(new object[] { "roads", 2, 2, 6, 0.0, 2 })]
	[InlineData(new object[] { "roads", 2, 2, 6, 1.0, 3 })]
	[InlineData(new object[] { "roads", 2, 2, 6, 5.0, 5 })]
	[InlineData(new object[] { "transport", 2, 2, 5, 0.0, 2 })]
	[InlineData(new object[] { "transport", 2, 2, 5, 1.0, 3 })]
	[InlineData(new object[] { "transport", 2, 2, 5, 5.0, 5 })]
	[InlineData(new object[] { "electricity", 2, 2, 6, 0.0, 2 })]
	[InlineData(new object[] { "electricity", 2, 2, 6, 1.0, 3 })]
	[InlineData(new object[] { "electricity", 2, 2, 6, 5.0, 5 })]
	[InlineData(new object[] { "water", 2, 2, 7, 0.0, 2 })]
	[InlineData(new object[] { "water", 2, 2, 7, 1.0, 3 })]
	[InlineData(new object[] { "water", 2, 2, 7, 5.0, 5 })]
	[InlineData(new object[] { "environment", 2, 2, 6, 0.0, 2 })]
	[InlineData(new object[] { "environment", 2, 2, 6, 1.0, 3 })]
	[InlineData(new object[] { "environment", 2, 2, 6, 5.0, 5 })]
	[InlineData(new object[] { "safety", 2, 2, 6, 0.0, 2 })]
	[InlineData(new object[] { "safety", 2, 2, 6, 1.0, 3 })]
	[InlineData(new object[] { "safety", 2, 2, 6, 5.0, 5 })]
	[InlineData(new object[] { "infrastructure", 2, 2, 6, 0.0, 2 })]
	[InlineData(new object[] { "infrastructure", 2, 2, 6, 1.0, 3 })]
	[InlineData(new object[] { "infrastructure", 2, 2, 6, 5.0, 5 })]
	public void ComputeMacf_VariousSds_ReturnsExpected(string categoryName, int baseFloor, int macfMin, int macfMax, double sds, int expected)
	{
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = baseFloor,
			MacfMin = macfMin,
			MacfMax = macfMax
		};
		int actual = CivisCalculator.ComputeMacf(sds, opts);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void ComputeMacf_SdsVeryHigh_ClampsToMacfMax()
	{
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = 2,
			MacfMin = 2,
			MacfMax = 6
		};
		Assert.Equal(6, CivisCalculator.ComputeMacf(100.0, opts));
	}

	[Fact]
	public void ComputeMacf_SdsZero_ReturnsBaseFloor()
	{
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = 2,
			MacfMin = 2,
			MacfMax = 6
		};
		Assert.Equal(2, CivisCalculator.ComputeMacf(0.0, opts));
	}

	[Fact]
	public void ComputeMacf_SafetyCategory_AppliesSensitivityUplift()
	{
		// base 2 + log2(1+1)=1 + safetyUplift 1 + 0 = 4 → ceil 4
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = 2,
			MacfMin = 2,
			MacfMax = 6
		};
		int nonSafety = CivisCalculator.ComputeMacf(1.0, opts, isSafetyCategory: false);
		int safety = CivisCalculator.ComputeMacf(1.0, opts, isSafetyCategory: true);
		Assert.Equal(3, nonSafety);
		Assert.Equal(4, safety);
	}

	[Fact]
	public void ComputeMacf_LowLocationConfidence_AppliesGeoUncertaintyUplift()
	{
		// base 2 + log2(1+1)=1 + 0 + (0.5 * 0.5)=0.25 → 3.25 → ceil 4
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = 2,
			MacfMin = 2,
			MacfMax = 6
		};
		int highConfidence = CivisCalculator.ComputeMacf(1.0, opts, locationConfidence: 1.0);
		int lowConfidence = CivisCalculator.ComputeMacf(1.0, opts, locationConfidence: 0.4);
		int boundary = CivisCalculator.ComputeMacf(1.0, opts, locationConfidence: 0.5);
		Assert.Equal(3, highConfidence);
		Assert.Equal(4, lowConfidence);
		// boundary: locationConfidence == 0.5 → NOT < 0.5, so no uplift
		Assert.Equal(3, boundary);
	}

	[Fact]
	public void ComputeMacf_SafetyAndLowConfidence_AppliesBothUplifts()
	{
		// base 2 + 1 + 1 + 0.25 = 4.25 → ceil 5
		CivisCategoryOptions opts = new CivisCategoryOptions
		{
			BaseFloor = 2,
			MacfMin = 2,
			MacfMax = 6
		};
		int actual = CivisCalculator.ComputeMacf(1.0, opts, isSafetyCategory: true, locationConfidence: 0.4);
		Assert.Equal(5, actual);
	}

	[Fact]
	public void ComputeSds_WrabBelowBaseFloor_UsesBaseFloor()
	{
		double actual = CivisCalculator.ComputeSds(4.0, 0.0, 2);
		Assert.Equal(2.0, actual, 6);
	}

	[Fact]
	public void ComputeSds_WrabAboveBaseFloor_UsesWrab()
	{
		double actual = CivisCalculator.ComputeSds(5.0, 10.0, 2);
		Assert.Equal(0.5, actual, 6);
	}

	[Theory]
	[InlineData(new object[] { 18 })]
	[InlineData(new object[] { 8 })]
	[InlineData(new object[] { 12 })]
	[InlineData(new object[] { 24 })]
	[InlineData(new object[] { 36 })]
	public void ApplyDecay_AtT0_ReturnsInitialMass(double halfLifeHours)
	{
		double lambda = CivisCalculator.ComputeLambda(halfLifeHours);
		double actual = CivisCalculator.ApplyDecay(10.0, lambda, 0.0);
		Assert.Equal(10.0, actual, 6);
	}

	[Theory]
	[InlineData(new object[] { 18 })]
	[InlineData(new object[] { 8 })]
	[InlineData(new object[] { 12 })]
	[InlineData(new object[] { 24 })]
	[InlineData(new object[] { 36 })]
	public void ApplyDecay_AtHalfLife_ReturnsHalfOfInitial(double halfLifeHours)
	{
		double lambda = CivisCalculator.ComputeLambda(halfLifeHours);
		double actual = CivisCalculator.ApplyDecay(10.0, lambda, halfLifeHours);
		Assert.Equal(5.0, actual, 4);
	}

	[Theory]
	[InlineData(new object[] { 18 })]
	[InlineData(new object[] { 8 })]
	[InlineData(new object[] { 12 })]
	[InlineData(new object[] { 24 })]
	[InlineData(new object[] { 36 })]
	public void ApplyDecay_AtTwoHalfLives_ReturnsQuarterOfInitial(double halfLifeHours)
	{
		double lambda = CivisCalculator.ComputeLambda(halfLifeHours);
		double actual = CivisCalculator.ApplyDecay(10.0, lambda, halfLifeHours * 2.0);
		Assert.Equal(2.5, actual, 4);
	}

	[Fact]
	public void ComputeLambda_HalfLifeOf24Hours_CorrectLambda()
	{
		double actual = CivisCalculator.ComputeLambda(24.0);
		Assert.Equal(Math.Log(2.0) / 24.0, actual, 10);
	}
}
