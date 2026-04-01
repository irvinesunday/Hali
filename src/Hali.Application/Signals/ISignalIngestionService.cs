using Hali.Contracts.Signals;

namespace Hali.Application.Signals;

public interface ISignalIngestionService
{
    Task<SignalPreviewResponseDto> PreviewAsync(SignalPreviewRequestDto request, CancellationToken ct = default);
    Task<SignalSubmitResponseDto> SubmitAsync(SignalSubmitRequestDto request, Guid? accountId, Guid? deviceId, CancellationToken ct = default);
}
