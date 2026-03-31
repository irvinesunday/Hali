namespace Hali.Domain.Entities.Clusters;

public class ClusterEventLink
{
    public Guid Id { get; set; }
    public Guid ClusterId { get; set; }
    public Guid SignalEventId { get; set; }
    public string? LinkReason { get; set; }
    public DateTime LinkedAt { get; set; }
}
