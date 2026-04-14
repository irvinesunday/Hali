using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Logging;
using Hali.Application.Auth;
using Hali.Application.Errors;
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
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        var account = await _auth.FindAccountByIdAsync(accountId, ct);
        if (account == null)
            throw new NotFoundException(
                code: "account.not_found",
                message: "Account not found.");

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
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        var account = await _auth.FindAccountByIdAsync(accountId, ct);
        if (account == null)
            throw new NotFoundException(
                code: "account.not_found",
                message: "Account not found.");

        account.NotificationSettings = JsonSerializer.Serialize(dto);
        account.UpdatedAt = DateTime.UtcNow;
        await _auth.UpdateAccountAsync(account, ct);

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        // Log a non-reversible hash of the account id rather than the raw
        // GUID — CodeQL treats raw identifiers in logs as PII (cs/cleartext-
        // storage-of-sensitive-information).
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} accountHash={AccountHash}",
            "account.notification_settings_updated", correlationId, AccountLogIdentifier.Hash(accountId));

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
