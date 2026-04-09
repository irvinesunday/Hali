using System;

namespace Hali.Application.Clusters;

public static class CivisCalculator
{
	// MACF formula per mvp_locked_decisions.md §9:
	//   rawMacf = BaseFloor
	//           + Alpha * Log2(1 + SDS)
	//           + SensitivityUplift            // 1 for safety, 0 otherwise
	//           + geoUncertainty * 0.5         // geoUncertainty = 0.5 when locationConfidence < 0.5, else 0
	// Alpha = 1.0 for all MVP categories.
	// Optional parameters default to safe values (non-safety, full confidence)
	// so existing callers and tests retain their previous behavior.
	public static int ComputeMacf(
		double sds,
		CivisCategoryOptions opts,
		bool isSafetyCategory = false,
		double locationConfidence = 1.0)
	{
		const double alpha = 1.0;
		double sensitivityUplift = isSafetyCategory ? 1.0 : 0.0;
		double geoUncertainty = locationConfidence < 0.5 ? 0.5 : 0.0;

		double rawMacf = (double)opts.BaseFloor
			+ alpha * Math.Log2(1.0 + sds)
			+ sensitivityUplift
			+ geoUncertainty * 0.5;

		int value = (int)Math.Ceiling(rawMacf);
		return Math.Clamp(value, opts.MacfMin, opts.MacfMax);
	}

	public static double ComputeSds(double activeMass, double wrab, int baseFloor)
	{
		double num = Math.Max(wrab, baseFloor);
		return activeMass / num;
	}

	public static double ComputeLambda(double halfLifeHours)
	{
		return Math.Log(2.0) / halfLifeHours;
	}

	public static double ApplyDecay(double initialMass, double lambda, double elapsedHours)
	{
		return initialMass * Math.Exp((0.0 - lambda) * elapsedHours);
	}
}
