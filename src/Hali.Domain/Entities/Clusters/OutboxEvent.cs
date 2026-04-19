using System;

namespace Hali.Domain.Entities.Clusters;

public class OutboxEvent
{
    public Guid Id { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = "1.0";

    public string? Payload { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime? PublishedAt { get; set; }
}
