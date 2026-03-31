namespace Hali.Domain.Entities.Advisories;

public class InstitutionJurisdiction
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid? LocalityId { get; set; }
    public string? CorridorName { get; set; }
    // geom handled as shadow property in EF config
    public DateTime CreatedAt { get; set; }
}
