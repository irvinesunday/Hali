using System;
using System.Diagnostics.Metrics;

namespace Hali.Api.Observability;

/// <summary>
/// Host for the top-level <c>Hali.Api</c> <see cref="Meter"/> and the instruments
/// emitted directly from API-tier cross-cutting concerns (middleware, filters).
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>Program.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour regresses.
/// </summary>
public sealed class ApiMetrics : IDisposable
{
    /// <summary>
    /// Name of the application-wide <see cref="Meter"/>. Mirrored by
    /// <c>AddMeter(ApiMetrics.MeterName)</c> on the OpenTelemetry meter provider.
    /// </summary>
    public const string MeterName = "Hali.Api";

    /// <summary>
    /// Name of the exception counter. Exposed as a const so tests and operators
    /// can reference the wire name without duplicating a string literal.
    /// </summary>
    public const string ApiExceptionsTotalName = "api_exceptions_total";

    /// <summary>Tag key carrying the mapped wire error code.</summary>
    public const string TagErrorCode = "error_code";

    /// <summary>Tag key carrying the mapped error category (lowercase).</summary>
    public const string TagErrorCategory = "error_category";

    /// <summary>Tag key carrying the HTTP status code written to the wire.</summary>
    public const string TagStatusCode = "status_code";

    private readonly Meter _meter;

    /// <summary>
    /// <c>api_exceptions_total</c> — incremented once per exception translated
    /// by <c>ExceptionHandlingMiddleware</c> into the canonical error envelope.
    ///
    /// Tags reflect the <b>wire-visible</b> mapping outcome, not the raw
    /// exception internals:
    /// <list type="bullet">
    ///   <item><description><c>error_code</c> — mapped <c>ErrorCodes</c> value.
    ///     Internal-only codes (e.g. <c>clustering.no_spatial_cell</c>) are
    ///     redacted to <c>server.internal_error</c> before tagging, matching
    ///     what the client actually receives.</description></item>
    ///   <item><description><c>error_category</c> — lowercase
    ///     <c>ErrorCategory</c> value. Redacted and unmapped exceptions tag as
    ///     <c>unexpected</c>.</description></item>
    ///   <item><description><c>status_code</c> — HTTP status code on the wire
    ///     (integer).</description></item>
    /// </list>
    ///
    /// All three dimensions are bounded by static catalogs (≤51 codes,
    /// 11 categories, a small set of status codes) so cardinality is
    /// controlled. No request-derived value (path, method, account id,
    /// correlation id) is ever attached.
    /// </summary>
    public Counter<long> ApiExceptionsTotal { get; }

    public ApiMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);
        ApiExceptionsTotal = _meter.CreateCounter<long>(
            name: ApiExceptionsTotalName,
            unit: "{exception}",
            description: "Number of exceptions translated into the canonical error envelope by ExceptionHandlingMiddleware.");
    }

    public void Dispose() => _meter.Dispose();
}
