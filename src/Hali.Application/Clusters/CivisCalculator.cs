namespace Hali.Application.Clusters;

/// <summary>
/// Pure CIVIS formula implementations. No dependencies — fully testable in isolation.
/// All formulas sourced from mvp_locked_decisions.md §9.
/// </summary>
public static class CivisCalculator
{
    /// <summary>
    /// MACF = clamp(ceil(base_floor + log2(SDS + 1)), macf_min, macf_max)
    /// </summary>
    public static int ComputeMacf(double sds, CivisCategoryOptions opts)
    {
        var raw = opts.BaseFloor + Math.Log2(sds + 1);
        var ceiled = (int)Math.Ceiling(raw);
        return Math.Clamp(ceiled, opts.MacfMin, opts.MacfMax);
    }

    /// <summary>
    /// SDS = active_mass_now / effective_WRAB
    /// effective_WRAB = max(WRAB, base_floor)
    /// </summary>
    public static double ComputeSds(double activeMass, double wrab, int baseFloor)
    {
        var effectiveWrab = Math.Max(wrab, baseFloor);
        return activeMass / effectiveWrab;
    }

    /// <summary>
    /// Lambda = ln(2) / half_life_hours
    /// </summary>
    public static double ComputeLambda(double halfLifeHours)
        => Math.Log(2) / halfLifeHours;

    /// <summary>
    /// Exponential decay: initial_mass * exp(-lambda * elapsed_hours)
    /// </summary>
    public static double ApplyDecay(double initialMass, double lambda, double elapsedHours)
        => initialMass * Math.Exp(-lambda * elapsedHours);
}
