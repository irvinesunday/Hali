using Hali.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Infrastructure.Auth;

public class AfricasTalkingSmsProvider : ISmsProvider
{
    private const string ApiUrl = "https://api.africastalking.com/version1/messaging";

    private readonly HttpClient _http;
    private readonly AfricasTalkingOptions _opts;
    private readonly ILogger<AfricasTalkingSmsProvider> _logger;

    public AfricasTalkingSmsProvider(
        HttpClient http,
        IOptions<AfricasTalkingOptions> opts,
        ILogger<AfricasTalkingSmsProvider> logger)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(string destination, string message, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _opts.Username,
            ["to"] = destination,
            ["message"] = message,
            ["from"] = _opts.SenderId
        });

        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("apiKey", _opts.ApiKey);
        _http.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await _http.PostAsync(ApiUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Africa's Talking SMS failed. Status: {Status}, Body: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"SMS delivery failed: {response.StatusCode}");
        }
    }
}
