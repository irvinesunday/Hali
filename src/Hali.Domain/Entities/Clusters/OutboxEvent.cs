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

    /// <summary>
    /// Identifies the request or trace chain that caused this event.
    /// Every outbox event carries a correlation id so the full chain
    /// of events from a single request can be grouped in logs and
    /// monitoring tools.
    /// </summary>
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The id of the direct parent event that caused this event, or
    /// null when the event was triggered by an external request rather
    /// than by another event in the outbox. Null is explicit — not
    /// an accidental omission.
    /// </summary>
    public Guid? CausationId { get; set; }
}
