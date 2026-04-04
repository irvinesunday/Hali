using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Enums;

namespace Hali.Application.Participation;

public interface IParticipationService
{
	Task RecordParticipationAsync(Guid clusterId, Guid deviceId, Guid? accountId, ParticipationType type, string? idempotencyKey, CancellationToken ct);

	Task AddContextAsync(Guid clusterId, Guid deviceId, string contextText, CancellationToken ct);

	Task RecordRestorationResponseAsync(Guid clusterId, Guid deviceId, Guid? accountId, string response, CancellationToken ct);
}
