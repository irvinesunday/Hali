using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;

namespace Hali.Contracts.Home;

public class HomeResponseDto
{
    public PagedSection<ClusterResponseDto> ActiveNow { get; set; } = EmptyCluster();
    public PagedSection<OfficialPostResponseDto> OfficialUpdates { get; set; } = EmptyPost();
    public PagedSection<ClusterResponseDto> RecurringAtThisTime { get; set; } = EmptyCluster();
    public PagedSection<ClusterResponseDto> OtherActiveSignals { get; set; } = EmptyCluster();

    private static PagedSection<ClusterResponseDto> EmptyCluster() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };

    private static PagedSection<OfficialPostResponseDto> EmptyPost() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };
}
