namespace Hali.Contracts.Notifications;

public class NotificationSettingsDto
{
    public bool ClusterActivated { get; set; } = true;
    public bool RestorationPrompt { get; set; } = true;
    public bool ClusterResolved { get; set; } = true;
}
