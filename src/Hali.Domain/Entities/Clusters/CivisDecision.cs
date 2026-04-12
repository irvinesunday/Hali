using System;

namespace Hali.Domain.Entities.Clusters;

public class CivisDecision
{
    public Guid Id { get; set; }

    public Guid ClusterId { get; set; }

    public string DecisionType { get; set; } = string.Empty;

    public string? ReasonCodes { get; set; }

    public string? Metrics { get; set; }

    public DateTime CreatedAt { get; set; }
}
