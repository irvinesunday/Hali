using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Feedback;
using Hali.Contracts.Requests;
using Hali.Domain.Entities.Feedback;
using Hali.Infrastructure.Data.Feedback;

namespace Hali.Infrastructure.Feedback;

/// <summary>
/// Writes <see cref="AppFeedback"/> rows via <see cref="FeedbackDbContext"/>.
///
/// Deliberately thin: no business logic, no rate limiting, no side-effects.
/// The controller enforces request-shape validation and the DB owns
/// length/shape constraints through EF configuration.
/// </summary>
public sealed class FeedbackService : IFeedbackService
{
    private readonly FeedbackDbContext _db;

    public FeedbackService(FeedbackDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> SubmitAsync(
        SubmitFeedbackRequest request,
        Guid? accountId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new AppFeedback
        {
            Id = Guid.NewGuid(),
            Rating = request.Rating,
            Text = request.Text,
            Screen = request.Screen,
            ClusterId = request.ClusterId,
            AccountId = accountId,
            AppVersion = request.AppVersion,
            Platform = request.Platform,
            SessionId = request.SessionId,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        _db.AppFeedback.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
