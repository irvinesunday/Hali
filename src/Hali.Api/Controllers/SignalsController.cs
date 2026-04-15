using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using StackExchange.Redis;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/signals")]
public class SignalsController : ControllerBase
{
    private readonly ISignalIngestionService _ingestion;

    private readonly IAuthRepository _auth;

    private readonly SignalsMetrics? _metrics;

    public SignalsController(
        ISignalIngestionService ingestion,
        IAuthRepository auth,
        SignalsMetrics? metrics = null)
    {
        _ingestion = ingestion;
        _auth = auth;
        _metrics = metrics;
    }

    [HttpPost("preview")]
    [AllowAnonymous]
    public async Task<IActionResult> Preview([FromBody] SignalPreviewRequestDto dto, CancellationToken ct, [FromServices] IConnectionMultiplexer redis)
    {
        // The request-outcome counter is incremented exactly once per non-
        // cancellation call. `emit` stays true on every normal code path and
        // flips to false only when OperationCanceledException propagates —
        // client disconnects are not an operational signal about the endpoint
        // itself, so they do not bias any bucket. The outcome string defaults
        // to dependency_error and is tightened by the catch clauses when the
        // exception kind is known; on the success path it is set just before
        // the happy-path return.
        string outcome = SignalsMetrics.OutcomeDependencyError;
        bool emit = true;
        try
        {
            // BLOCKING-4 fix: rate limit anonymous NLP previews (10/IP/10min)
            var _previewIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var _previewKey = $"rl:signal-preview:{_previewIp}";
            var _previewDb = redis.GetDatabase();
            var _previewCount = await _previewDb.StringIncrementAsync(_previewKey);
            if (_previewCount == 1) await _previewDb.KeyExpireAsync(_previewKey, TimeSpan.FromMinutes(10));
            if (_previewCount > 10)
            {
                // Rate limit → 429. Bucketed as validation_error so dashboards
                // don't alert on user-behaviour-driven spikes; dependency_error
                // is reserved for actual server/dependency failures.
                outcome = SignalsMetrics.OutcomeValidationError;
                throw new RateLimitException(ErrorCodes.RateLimitExceeded, "Too many preview requests.");
            }

            if (string.IsNullOrWhiteSpace(dto.FreeText))
            {
                outcome = SignalsMetrics.OutcomeValidationError;
                throw new ValidationException("free_text is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["free_text"] = ["free_text is required."]
                    });
            }

            var response = await _ingestion.PreviewAsync(dto, ct);
            outcome = SignalsMetrics.OutcomeSuccess;
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            emit = false;
            throw;
        }
        catch (ValidationException)
        {
            outcome = SignalsMetrics.OutcomeValidationError;
            throw;
        }
        catch (RateLimitException)
        {
            outcome = SignalsMetrics.OutcomeValidationError;
            throw;
        }
        catch (DependencyException)
        {
            outcome = SignalsMetrics.OutcomeDependencyError;
            throw;
        }
        catch
        {
            // Any other exception (including unmapped AppException subclasses
            // and non-AppException exceptions translated to 500 by
            // ExceptionHandlingMiddleware) lands in the dependency bucket —
            // the default already set above.
            throw;
        }
        finally
        {
            if (emit)
            {
                _metrics?.SignalsPreviewRequestsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(SignalsMetrics.TagOutcome, outcome));
            }
        }
    }

    [HttpPost("submit")]
    [Authorize]
    public async Task<IActionResult> Submit([FromBody] SignalSubmitRequestDto dto, CancellationToken ct)
    {
        // Same contract as Preview above — increment once, on every non-
        // cancellation path, with a bounded outcome.
        string outcome = SignalsMetrics.OutcomeDependencyError;
        bool emit = true;
        try
        {
            if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            {
                outcome = SignalsMetrics.OutcomeValidationError;
                throw new ValidationException("idempotency_key is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["idempotency_key"] = ["idempotency_key is required."]
                    });
            }
            if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            {
                outcome = SignalsMetrics.OutcomeValidationError;
                throw new ValidationException("device_hash is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["device_hash"] = ["device_hash is required."]
                    });
            }
            Guid? accountId = null;
            string sub = base.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (Guid.TryParse(sub, out var parsed))
            {
                accountId = parsed;
            }
            Device device = ((!string.IsNullOrWhiteSpace(dto.DeviceHash)) ? (await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct)) : null);
            Device device2 = device;

            var response = await _ingestion.SubmitAsync(dto, accountId, device2?.Id, ct);
            outcome = SignalsMetrics.OutcomeSuccess;
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            emit = false;
            throw;
        }
        catch (ValidationException)
        {
            outcome = SignalsMetrics.OutcomeValidationError;
            throw;
        }
        catch (ConflictException)
        {
            // Duplicate idempotency key — a user-visible 409, not a server
            // failure. Bucketed with other validation rejections.
            outcome = SignalsMetrics.OutcomeValidationError;
            throw;
        }
        catch (RateLimitException)
        {
            outcome = SignalsMetrics.OutcomeValidationError;
            throw;
        }
        catch (DependencyException)
        {
            outcome = SignalsMetrics.OutcomeDependencyError;
            throw;
        }
        catch
        {
            throw;
        }
        finally
        {
            if (emit)
            {
                _metrics?.SignalsSubmitRequestsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(SignalsMetrics.TagOutcome, outcome));
            }
        }
    }
}
