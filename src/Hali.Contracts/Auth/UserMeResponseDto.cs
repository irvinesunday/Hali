using System;
using Hali.Contracts.Notifications;

namespace Hali.Contracts.Auth;

public class UserMeResponseDto
{
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneE164 { get; set; }
    public string? Email { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public NotificationSettingsDto NotificationSettings { get; set; } = new();
}
