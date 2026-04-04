using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Signals;

namespace Hali.Application.Signals;

public interface ISignalIngestionService
{
	Task<SignalPreviewResponseDto> PreviewAsync(SignalPreviewRequestDto request, CancellationToken ct = default(CancellationToken));

	Task<SignalSubmitResponseDto> SubmitAsync(SignalSubmitRequestDto request, Guid? accountId, Guid? deviceId, CancellationToken ct = default(CancellationToken));
}
