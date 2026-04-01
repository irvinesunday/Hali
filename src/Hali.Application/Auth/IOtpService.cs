using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Enums;

namespace Hali.Application.Auth;

public interface IOtpService
{
	Task RequestOtpAsync(string destination, AuthMethod authMethod = AuthMethod.PhoneOtp, CancellationToken ct = default(CancellationToken));

	Task<bool> ConsumeOtpAsync(string destination, string otp, CancellationToken ct = default(CancellationToken));
}
