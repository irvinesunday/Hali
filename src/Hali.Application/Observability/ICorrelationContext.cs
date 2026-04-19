using System;

namespace Hali.Application.Observability;

/// <summary>
/// Provides access to the correlation id for the current request or worker scope.
/// Inject this interface wherever an outbox event is created so the event carries
/// the live request's correlation id rather than a fresh, disconnected GUID.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// Gets the correlation id for the current request scope.
    /// Returns <see cref="Guid.Empty"/> when there is no active HTTP context
    /// (e.g. unit tests without a configured accessor).
    /// </summary>
    Guid CurrentCorrelationId { get; }

    /// <summary>
    /// Generates a new root correlation id to be used when a worker has no
    /// parent outbox event to propagate from (e.g. periodic / scheduled jobs).
    /// Callers that receive an outbox event with a non-empty correlation id
    /// must propagate that value — they must NOT call this method.
    /// </summary>
    Guid CreateNewCorrelationId();
}
