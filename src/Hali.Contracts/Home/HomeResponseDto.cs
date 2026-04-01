using System.Collections.Generic;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;

namespace Hali.Contracts.Home;

public class HomeResponseDto
{
    public List<ClusterResponseDto> ActiveNow { get; set; } = new();
    public List<OfficialPostResponseDto> OfficialUpdates { get; set; } = new();
}
