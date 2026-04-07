using System;
using System.Collections.Generic;

namespace Hali.Contracts.Notifications;

public class FollowedLocalitiesRequestDto
{
    /// <summary>
    /// Either provide LocalityIds (legacy bulk replace) OR Items
    /// (preferred — carries displayLabel for each follow).
    /// When Items is non-empty it takes precedence and LocalityIds is ignored.
    /// </summary>
    public List<Guid> LocalityIds { get; set; } = new();

    public List<FollowedLocalityItemDto> Items { get; set; } = new();
}

public class FollowedLocalityItemDto
{
    public Guid LocalityId { get; set; }
    public string? DisplayLabel { get; set; }
}
