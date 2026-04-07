using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

    /// <summary>
    /// Optional human-readable label for the follow. Persisted to the
    /// follows.display_label column which is HasMaxLength(160).
    /// Validated at the API boundary so over-length input returns 400
    /// instead of triggering a DB exception.
    /// </summary>
    [StringLength(160)]
    public string? DisplayLabel { get; set; }
}
