namespace Hali.Contracts.Clusters;

public record ParticipationRequestDto(
    string Type,
    string DeviceHash,
    string? IdempotencyKey
);
