using System;
using System.Collections.Generic;

namespace Hali.Contracts.Notifications;

public class FollowedLocalitiesRequestDto
{
    public List<Guid> LocalityIds { get; set; } = new();
}
