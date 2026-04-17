using System;

namespace Hali.Contracts.Notifications;

public class LocalitySearchResultDto
{
    public Guid LocalityId { get; set; }
    public string PlaceLabel { get; set; } = string.Empty;
    public string WardName { get; set; } = string.Empty;
    public string? CityName { get; set; }
}
