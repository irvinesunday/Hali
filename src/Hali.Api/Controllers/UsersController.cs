using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Hali.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthRepository _auth;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IAuthRepository auth, ILogger<UsersController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var accountId = GetAccountId();
        if (accountId == null)
            return Unauthorized();

        var account = await _auth.FindAccountByIdAsync(accountId.Value, ct);
        if (account == null)
            return NotFound();

        var settings = ParseNotificationSettings(account.NotificationSettings);

        return Ok(new UserMeResponseDto
        {
            Id = account.Id,
            DisplayName = account.DisplayName,
            PhoneE164 = account.PhoneE164,
            Email = account.Email,
            Status = account.Status,
            CreatedAt = account.CreatedAt,
            NotificationSettings = settings
        });
    }

    [HttpPut("me/notification-settings")]
    public async Task<IActionResult> UpdateNotificationSettings(
        [FromBody] NotificationSettingsDto dto,
        CancellationToken ct)
    {
        var accountId = GetAccountId();
        if (accountId == null)
            return Unauthorized();

        var account = await _auth.FindAccountByIdAsync(accountId.Value, ct);
        if (account == null)
            return NotFound();

        account.NotificationSettings = JsonSerializer.Serialize(dto);
        account.UpdatedAt = DateTime.UtcNow;
        await _auth.UpdateAccountAsync(account, ct);

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} accountId={AccountId}",
            "account.notification_settings_updated", correlationId, accountId);

        return NoContent();
    }

    private Guid? GetAccountId()
    {
        var raw = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static NotificationSettingsDto ParseNotificationSettings(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new NotificationSettingsDto();
        try
        {
            return JsonSerializer.Deserialize<NotificationSettingsDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new NotificationSettingsDto();
        }
        catch
        {
            return new NotificationSettingsDto();
        }
    }
}
