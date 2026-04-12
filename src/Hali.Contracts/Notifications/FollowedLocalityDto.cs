using System;

namespace Hali.Contracts.Notifications;

public class FollowedLocalityDto
{
    public Guid LocalityId { get; set; }
    public string? DisplayLabel { get; set; }
    public string WardName { get; set; } = string.Empty;
    public string? CityName { get; set; }
}
