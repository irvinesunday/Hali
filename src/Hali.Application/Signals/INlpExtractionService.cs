using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Signals;

public interface INlpExtractionService
{
    Task<NlpExtractionResultDto?> ExtractAsync(NlpExtractionRequest request, CancellationToken ct = default(CancellationToken));
}
