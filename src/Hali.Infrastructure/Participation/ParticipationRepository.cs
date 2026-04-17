using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Participation;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Participation;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Participation;

public class ParticipationRepository : IParticipationRepository
{
	private readonly ParticipationDbContext _db;

	public ParticipationRepository(ParticipationDbContext db)
	{
		_db = db;
	}

	public Task<Hali.Domain.Entities.Participation.Participation?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
	{
		return (from p in _db.Participations
			where p.ClusterId == clusterId && p.DeviceId == deviceId
			orderby p.CreatedAt descending
			select p).FirstOrDefaultAsync(ct);
	}

	public Task<Hali.Domain.Entities.Participation.Participation?> GetMostRecentByAccountAsync(Guid clusterId, Guid accountId, CancellationToken ct)
	{
		return (from p in _db.Participations
			where p.ClusterId == clusterId && p.AccountId == accountId
			orderby p.CreatedAt descending
			select p).FirstOrDefaultAsync(ct);
	}

	public async Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
	{
		List<Hali.Domain.Entities.Participation.Participation> existing = await _db.Participations.Where((Hali.Domain.Entities.Participation.Participation p) => p.ClusterId == clusterId && p.DeviceId == deviceId).ToListAsync(ct);
		if (existing.Count > 0)
		{
			_db.Participations.RemoveRange(existing);
			await _db.SaveChangesAsync(ct);
		}
	}

	public async Task AddAsync(Hali.Domain.Entities.Participation.Participation participation, CancellationToken ct)
	{
		_db.Participations.Add(participation);
		await _db.SaveChangesAsync(ct);
	}

	public async Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct)
	{
		Hali.Domain.Entities.Participation.Participation p = await _db.Participations.FindAsync(new object[1] { participationId }, ct);
		if (p != null)
		{
			p.ContextText = contextText;
			await _db.SaveChangesAsync(ct);
		}
	}

	public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
	{
		return _db.Participations.CountAsync((Hali.Domain.Entities.Participation.Participation p) => p.ClusterId == clusterId && (int)p.ParticipationType == (int)type, ct);
	}

	public async Task<RestorationCountSnapshot> GetRestorationCountSnapshotAsync(Guid clusterId, CancellationToken ct)
	{
		// Single EF query: GroupBy + conditional Count over the cluster's
		// participation rows. Replaces the prior two-query pattern
		// (CountByType(RestorationYes) followed by CountRestorationResponses)
		// which exposed a race where a concurrent participation-type flip
		// could produce yesVotes > totalResponses or a ratio > 1. See #143.
		RestorationCountSnapshot? snapshot = await _db.Participations
			.Where(p => p.ClusterId == clusterId)
			.GroupBy(_ => 1)
			.Select(g => new RestorationCountSnapshot(
				g.Count(p => p.ParticipationType == ParticipationType.RestorationYes),
				g.Count(p => p.ParticipationType == ParticipationType.RestorationNo),
				g.Count(p => p.ParticipationType == ParticipationType.RestorationYes
					|| p.ParticipationType == ParticipationType.RestorationNo
					|| p.ParticipationType == ParticipationType.RestorationUnsure)))
			.FirstOrDefaultAsync(ct);
		return snapshot ?? new RestorationCountSnapshot(0, 0, 0);
	}

	public async Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct)
	{
		return await _db.Participations
			.Where(p => p.ClusterId == clusterId
				&& (int)p.ParticipationType == (int)ParticipationType.Affected
				&& p.AccountId != null)
			.Select(p => p.AccountId!.Value)
			.Distinct()
			.ToListAsync(ct);
	}
}
