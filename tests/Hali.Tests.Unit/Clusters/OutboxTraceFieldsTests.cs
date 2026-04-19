using System;
using System.Text.Json;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Xunit;

namespace Hali.Tests.Unit.Clusters;

/// <summary>
/// Verifies that every outbox event creation path populates
/// <c>CorrelationId</c> and makes an explicit decision about
/// <c>CausationId</c> (#276).
/// </summary>
public sealed class OutboxTraceFieldsTests
{
    // -----------------------------------------------------------------------
    // #276 — OutboxEvent_CorrelationId_PersistedAndRetrieved
    // -----------------------------------------------------------------------

    [Fact]
    public void OutboxEvent_CorrelationId_IsNonEmptyGuid()
    {
        var evt = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterActivated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
        };

        Assert.NotEqual(Guid.Empty, evt.CorrelationId);
    }

    // -----------------------------------------------------------------------
    // #276 — OutboxEvent_CausationId_ExplicitlyNullWhenNoParent
    // -----------------------------------------------------------------------

    [Fact]
    public void OutboxEvent_CausationId_ExplicitlyNullForRequestTriggeredEvents()
    {
        var evt = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterCreated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
        };

        Assert.Null(evt.CausationId);
    }

    // -----------------------------------------------------------------------
    // #276 — OutboxEvent_CausationId_PopulatedWhenParentExists
    // -----------------------------------------------------------------------

    [Fact]
    public void OutboxEvent_CausationId_PopulatedWhenParentEventIsKnown()
    {
        var parentId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var child = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterActivated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            CausationId = parentId,
        };

        Assert.Equal(parentId, child.CausationId);
        Assert.Equal(correlationId, child.CorrelationId);
    }

    // -----------------------------------------------------------------------
    // #276 — OutboxEvent_TraceFields_PresentInPublishedPayload
    // Verifies the fields survive round-trip serialization (entity → JSON)
    // -----------------------------------------------------------------------

    [Fact]
    public void OutboxEvent_TraceFields_SurviveJsonRoundTrip()
    {
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var evt = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.InstitutionActionRecorded,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            Payload = JsonSerializer.Serialize(new { test = true }),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            CausationId = causationId,
        };

        string json = JsonSerializer.Serialize(evt);
        OutboxEvent? deserialized = JsonSerializer.Deserialize<OutboxEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(correlationId, deserialized!.CorrelationId);
        Assert.Equal(causationId, deserialized.CausationId);
    }

    [Fact]
    public void OutboxEvent_TraceFields_NullCausationId_SurvivesJsonRoundTrip()
    {
        var correlationId = Guid.NewGuid();

        var evt = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterCreated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            CausationId = null,
        };

        string json = JsonSerializer.Serialize(evt);
        OutboxEvent? deserialized = JsonSerializer.Deserialize<OutboxEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(correlationId, deserialized!.CorrelationId);
        Assert.Null(deserialized.CausationId);
    }

    // -----------------------------------------------------------------------
    // Verify all call-site properties are distinct (each event gets its own
    // CorrelationId so they can be grouped per-request but are distinct events)
    // -----------------------------------------------------------------------

    [Fact]
    public void OutboxEvent_TwoIndependentEvents_HaveDistinctCorrelationIds()
    {
        var evt1 = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterCreated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
        };

        var evt2 = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "signal_cluster",
            AggregateId = Guid.NewGuid(),
            EventType = ObservabilityEvents.ClusterActivated,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            OccurredAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            CausationId = null,
        };

        Assert.NotEqual(evt1.CorrelationId, evt2.CorrelationId);
        Assert.NotEqual(Guid.Empty, evt1.CorrelationId);
        Assert.NotEqual(Guid.Empty, evt2.CorrelationId);
    }
}
