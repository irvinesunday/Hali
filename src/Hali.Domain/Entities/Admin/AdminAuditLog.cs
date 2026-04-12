using System;

namespace Hali.Domain.Entities.Admin;

public class AdminAuditLog
{
    public Guid Id { get; set; }

    public Guid? ActorAccountId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }
}
