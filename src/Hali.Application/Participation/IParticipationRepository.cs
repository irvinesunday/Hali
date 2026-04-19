using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;

namespace Hali.Application.Participation;

/// <summary>
/// Atomic point-in-time snapshot of restoration vote counts for a cluster.
/// Computed in a single database read so callers can rely on the invariants
/// <c>YesVotes + NoVotes &lt;= TotalResponses</c> and (when <c>TotalResponses &gt; 0</c>)
/// <c>YesVotes / TotalResponses ∈ [0, 1]</c>. <c>TotalResponses</c> is the sum
/// of all rows whose <see cref="ParticipationType"/> is one of
/// <c>RestorationYes</c>, <c>RestorationNo</c>, or <c>RestorationUnsure</c>.
/// </summary>
public sealed record RestorationCountSnapshot(int YesVotes, int NoVotes, int TotalResponses);

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

	/// <summary>
	/// Returns an atomic snapshot of restoration vote counts for the given
	/// cluster: <c>YesVotes</c>, <c>NoVotes</c>, and <c>TotalResponses</c>
	/// (yes + no + unsure). Computed as a single database read so that
	/// concurrent participation-type flips cannot produce
	/// <c>YesVotes &gt; TotalResponses</c> or a ratio outside <c>[0, 1]</c>.
	/// </summary>
	Task<RestorationCountSnapshot> GetRestorationCountSnapshotAsync(Guid clusterId, CancellationToken ct);

	/// <summary>
	/// Bulk variant of <see cref="GetRestorationCountSnapshotAsync"/>:
	/// returns snapshots for every cluster id in <paramref name="clusterIds"/>
	/// as a single database read. Used by the institution restoration
	/// queue to avoid the per-row N+1 query pattern Copilot flagged on
	/// #207. Clusters with no recorded restoration responses are omitted
	/// from the dictionary — callers should materialise the zero-count
	/// snapshot themselves.
	/// </summary>
	Task<IReadOnlyDictionary<Guid, RestorationCountSnapshot>> GetRestorationCountSnapshotsAsync(
		IReadOnlyCollection<Guid> clusterIds, CancellationToken ct);

	/// <summary>Returns account IDs of all users with an active affected participation on this cluster.</summary>
	Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct);
}
