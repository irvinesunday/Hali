using Hali.Domain.Enums;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;

namespace Hali.Application.Participation;

public interface IParticipationRepository
{
    Task<ParticipationEntity?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct);
    Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct);
    Task AddAsync(ParticipationEntity participation, CancellationToken ct);
    Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct);
    Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct);
    Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct);
}
