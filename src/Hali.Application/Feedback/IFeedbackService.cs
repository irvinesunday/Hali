using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Requests;

namespace Hali.Application.Feedback;

/// <summary>
/// Application-layer service that persists anonymous in-app feedback
/// submitted via <c>POST /v1/feedback</c>.
///
/// The endpoint is anonymous: <paramref name="accountId"/> is supplied only
/// when the caller presents a valid bearer token, otherwise null. No
/// exceptions are thrown for dropped optional fields — validation of the
/// request shape is enforced at the controller boundary via
/// <c>SubmitFeedbackRequest</c> DataAnnotations.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Persists a single feedback submission. Returns the assigned
    /// <c>id</c> of the stored row.
    /// </summary>
    Task<Guid> SubmitAsync(
        SubmitFeedbackRequest request,
        Guid? accountId,
        CancellationToken ct = default);
}
