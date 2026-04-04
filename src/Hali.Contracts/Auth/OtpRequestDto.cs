using Hali.Domain.Enums;

namespace Hali.Contracts.Auth;

public record OtpRequestDto(string Destination, AuthMethod AuthMethod);
