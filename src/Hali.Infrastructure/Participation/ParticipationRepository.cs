using Hali.Application.Participation;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Participation;
using Microsoft.EntityFrameworkCore;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;

namespace Hali.Infrastructure.Participation;

public class ParticipationRepository : IParticipationRepository
{
    private readonly ParticipationDbContext _db;

    public ParticipationRepository(ParticipationDbContext db) => _db = db;

    public Task<ParticipationEntity?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
        => _db.Participations
            .Where(p => p.ClusterId == clusterId && p.DeviceId == deviceId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
    {
        var existing = await _db.Participations
            .Where(p => p.ClusterId == clusterId && p.DeviceId == deviceId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            _db.Participations.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task AddAsync(ParticipationEntity participation, CancellationToken ct)
    {
        _db.Participations.Add(participation);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct)
    {
        var p = await _db.Participations.FindAsync([participationId], ct);
        if (p is null) return;
        p.ContextText = contextText;
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
        => _db.Participations
            .CountAsync(p => p.ClusterId == clusterId && p.ParticipationType == type, ct);

    public Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct)
        => _db.Participations
            .CountAsync(p => p.ClusterId == clusterId
                && (p.ParticipationType == ParticipationType.RestorationYes
                    || p.ParticipationType == ParticipationType.RestorationNo
                    || p.ParticipationType == ParticipationType.RestorationUnsure), ct);
}
