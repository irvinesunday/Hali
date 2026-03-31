namespace Hali.Domain.Entities.Advisories;

public class Institution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? JurisdictionLabel { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
