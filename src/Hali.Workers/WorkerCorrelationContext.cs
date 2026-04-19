using System;
using Hali.Application.Observability;

namespace Hali.Workers;

/// <summary>
/// No-HTTP-context implementation of <see cref="ICorrelationContext"/> for use
/// in the worker host. Workers have no inbound HTTP request, so there is no
/// request-scoped correlation id to propagate.
///
/// <see cref="CurrentCorrelationId"/> always returns <see cref="Guid.Empty"/>,
/// which signals to callers that they must apply the worker correlation rule:
/// propagate an existing non-empty correlation id from the outbox event being
/// processed, or generate a new root guid via <see cref="CreateNewCorrelationId"/>
/// when none exists (periodic/scheduled jobs with no parent event).
/// </summary>
internal sealed class WorkerCorrelationContext : ICorrelationContext
{
    /// <inheritdoc />
    /// <remarks>
    /// Always returns <see cref="Guid.Empty"/> in the worker host — there is
    /// no active HTTP context from which to read a correlation id. Callers
    /// must check for <see cref="Guid.Empty"/> and apply the worker
    /// correlation rule.
    /// </remarks>
    public Guid CurrentCorrelationId => Guid.Empty;

    /// <inheritdoc />
    public Guid CreateNewCorrelationId() => Guid.NewGuid();
}
