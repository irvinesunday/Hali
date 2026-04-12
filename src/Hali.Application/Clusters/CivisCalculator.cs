using System;

namespace Hali.Application.Clusters;

public static class CivisCalculator
{
    public static int ComputeMacf(double sds, CivisCategoryOptions opts)
    {
        double a = (double)opts.BaseFloor + Math.Log2(sds + 1.0);
        int value = (int)Math.Ceiling(a);
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
