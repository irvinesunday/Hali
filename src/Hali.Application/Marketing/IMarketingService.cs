using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Marketing;

namespace Hali.Application.Marketing;

public interface IMarketingService
{
    Task<Guid> RecordSignupAsync(SubmitSignupRequestDto request, CancellationToken ct = default);

    Task<Guid> RecordInquiryAsync(SubmitInquiryRequestDto request, CancellationToken ct = default);
}
