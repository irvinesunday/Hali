using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Marketing;
using Hali.Contracts.Marketing;
using Hali.Domain.Entities.Marketing;
using Hali.Infrastructure.Data.Marketing;

namespace Hali.Infrastructure.Marketing;

/// <summary>
/// Persists early access signups and institution inquiries via
/// <see cref="MarketingDbContext"/>. Deliberately thin — validation
/// is the controller's responsibility; this class owns only the
/// durable write.
/// </summary>
public sealed class MarketingService : IMarketingService
{
    private readonly MarketingDbContext _db;

    public MarketingService(MarketingDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> RecordSignupAsync(SubmitSignupRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new EarlyAccessSignup
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            SubmittedAt = DateTime.UtcNow,
        };

        _db.EarlyAccessSignups.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<Guid> RecordInquiryAsync(SubmitInquiryRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new InstitutionInquiry
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Organisation = request.Organisation.Trim(),
            Role = request.Role.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Area = request.Area.Trim(),
            Category = request.Category.Trim(),
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            SubmittedAt = DateTime.UtcNow,
        };

        _db.InstitutionInquiries.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
