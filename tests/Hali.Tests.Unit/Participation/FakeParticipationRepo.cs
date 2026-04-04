using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Participation;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;

namespace Hali.Tests.Unit.Participation;

internal sealed class FakeParticipationRepo : IParticipationRepository
{
	private readonly List<Hali.Domain.Entities.Participation.Participation> _store = new List<Hali.Domain.Entities.Participation.Participation>();

	public List<Hali.Domain.Entities.Participation.Participation> All => _store;

	public Task<Hali.Domain.Entities.Participation.Participation?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
	{
		Hali.Domain.Entities.Participation.Participation result = (from x in _store
			where x.ClusterId == clusterId && x.DeviceId == deviceId
			orderby x.CreatedAt descending
			select x).FirstOrDefault();
		return Task.FromResult(result);
	}

	public Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
	{
		_store.RemoveAll((Hali.Domain.Entities.Participation.Participation x) => x.ClusterId == clusterId && x.DeviceId == deviceId);
		return Task.CompletedTask;
	}

	public Task AddAsync(Hali.Domain.Entities.Participation.Participation participation, CancellationToken ct)
	{
		_store.Add(participation);
		return Task.CompletedTask;
	}

	public Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct)
	{
		Hali.Domain.Entities.Participation.Participation participation = _store.FirstOrDefault((Hali.Domain.Entities.Participation.Participation x) => x.Id == participationId);
		if (participation != null)
		{
			participation.ContextText = contextText;
		}
		return Task.CompletedTask;
	}

	public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
	{
		return Task.FromResult(_store.Count((Hali.Domain.Entities.Participation.Participation x) => x.ClusterId == clusterId && x.ParticipationType == type));
	}

	public Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct)
	{
		return Task.FromResult(_store.Count((Hali.Domain.Entities.Participation.Participation x) => x.ClusterId == clusterId && (x.ParticipationType == ParticipationType.RestorationYes || x.ParticipationType == ParticipationType.RestorationNo || x.ParticipationType == ParticipationType.RestorationUnsure)));
	}

	public Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct)
	{
		IReadOnlyList<Guid> result = _store
			.Where(x => x.ClusterId == clusterId && x.ParticipationType == ParticipationType.Affected && x.AccountId != null)
			.Select(x => x.AccountId!.Value)
			.Distinct()
			.ToList();
		return Task.FromResult(result);
	}
}
