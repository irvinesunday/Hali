namespace Hali.Application.Auth;

public interface ISmsProvider
{
    Task SendAsync(string destination, string message, CancellationToken ct = default);
}
