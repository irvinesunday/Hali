using System;
using System.Linq;
using H3;
using H3.Algorithms;
using Hali.Application.Clusters;

namespace Hali.Infrastructure.Clusters;

public class H3CellService : IH3CellService
{
	public string[] GetKRingCells(string h3CellId, int k)
	{
		H3Index origin = Convert.ToUInt64(h3CellId, 16);
		return (from c in origin.GridDiskDistances(k)
			select c.Index.ToString()).ToArray();
	}
}
