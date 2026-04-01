using H3;
using H3.Algorithms;
using Hali.Application.Clusters;

namespace Hali.Infrastructure.Clusters;

public class H3CellService : IH3CellService
{
    public string[] GetKRingCells(string h3CellId, int k)
    {
        var index = (H3Index)Convert.ToUInt64(h3CellId, 16);
        return Rings.GridDiskDistances(index, k)
            .Select(c => c.Index.ToString())
            .ToArray();
    }
}
