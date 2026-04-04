using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Advisories;

namespace Hali.Application.Advisories;

public interface IOfficialPostsService
{
    Task<OfficialPostResponseDto> CreatePostAsync(Guid institutionId, Guid? authorAccountId, CreateOfficialPostRequestDto dto, CancellationToken ct);
    Task<List<OfficialPostResponseDto>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct);
    Task<List<OfficialPostResponseDto>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct);
}
