using System;
using Hali.Application.Observability;
using Microsoft.AspNetCore.Http;

namespace Hali.Api.Observability;

/// <summary>
/// HTTP-context-backed implementation of <see cref="ICorrelationContext"/>.
/// Reads the sanitized, server-generated correlation id that
/// <see cref="Hali.Api.Middleware.CorrelationIdMiddleware"/> stores in
/// <c>HttpContext.Items["CorrelationId"]</c>. When there is no active HTTP
/// context (background scopes, unit tests without a configured accessor),
/// <see cref="CurrentCorrelationId"/> returns <see cref="Guid.Empty"/> so
/// callers can detect the absence and generate a root if needed.
/// </summary>
public sealed class CorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <inheritdoc />
    public Guid CurrentCorrelationId
    {
        get
        {
            var context = _accessor.HttpContext;
            if (context is null)
                return Guid.Empty;

            var raw = context.Items["CorrelationId"] as string;
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public Guid CreateNewCorrelationId() => Guid.NewGuid();
}
