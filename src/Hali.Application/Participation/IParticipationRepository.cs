using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;

namespace Hali.Application.Participation;

public interface IParticipationRepository
{
	Task<Hali.Domain.Entities.Participation.Participation?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct);

	/// <summary>
	/// Returns the caller's most recent participation row on a cluster,
	/// looked up by account_id. Used by GET /v1/clusters/{id} to populate
	/// the myParticipation field for the authenticated caller.
	/// </summary>
	Task<Hali.Domain.Entities.Participation.Participation?> GetMostRecentByAccountAsync(Guid clusterId, Guid accountId, CancellationToken ct);

	Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct);

	Task AddAsync(Hali.Domain.Entities.Participation.Participation participation, CancellationToken ct);

	Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct);

	Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct);

	Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct);

	/// <summary>Returns account IDs of all users with an active affected participation on this cluster.</summary>
	Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct);
}
