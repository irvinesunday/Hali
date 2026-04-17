using Hali.Domain.Enums;

namespace Hali.Application.Clusters;

public class CivisOptions
{
    public const string Section = "Civis";

    public int WrabRollingWindowDays { get; set; } = 30;

    public double JoinThreshold { get; set; } = 0.65;

    public int MinUniqueDevices { get; set; } = 2;

    public double DeactivationThreshold { get; set; } = 0.5;

    public int ActiveMassHorizonHours { get; set; } = 48;

    public double TimeScoreMaxAgeHours { get; set; } = 24.0;

    public int ContextEditWindowMinutes { get; set; } = 2;

    public double RestorationRatio { get; set; } = 0.6;

    public int MinRestorationAffectedVotes { get; set; } = 2;

    public CivisCategoryOptions Roads { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 18.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions Transport { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 8.0,
        MacfMin = 2,
        MacfMax = 5
    };

    public CivisCategoryOptions Electricity { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 12.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions Water { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 24.0,
        MacfMin = 2,
        MacfMax = 7
    };

    public CivisCategoryOptions Environment { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 36.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions Safety { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 18.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions Infrastructure { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 24.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions Governance { get; set; } = new CivisCategoryOptions
    {
        BaseFloor = 2,
        HalfLifeHours = 24.0,
        MacfMin = 2,
        MacfMax = 6
    };

    public CivisCategoryOptions GetCategoryOptions(CivicCategory category)
    {
        if (1 == 0)
        {
        }
        CivisCategoryOptions result = category switch
        {
            CivicCategory.Roads => Roads,
            CivicCategory.Transport => Transport,
            CivicCategory.Electricity => Electricity,
            CivicCategory.Water => Water,
            CivicCategory.Environment => Environment,
            CivicCategory.Safety => Safety,
            CivicCategory.Infrastructure => Infrastructure,
            CivicCategory.Governance => Governance,
            _ => new CivisCategoryOptions(),
        };
        if (1 == 0)
        {
        }
        return result;
    }
}
