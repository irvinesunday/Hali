using System;

namespace Hali.Contracts.Signals;

public record SignalSubmitResponseDto(
    Guid SignalEventId,
    Guid ClusterId,
    bool IsNewCluster,
    string ClusterState,
    Guid? LocalityId,
    DateTime CreatedAt);
