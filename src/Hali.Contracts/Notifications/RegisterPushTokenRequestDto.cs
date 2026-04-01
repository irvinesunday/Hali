namespace Hali.Contracts.Notifications;

public class RegisterPushTokenRequestDto
{
    public string ExpoPushToken { get; set; } = string.Empty;
    public string DeviceHash { get; set; } = string.Empty;
}
