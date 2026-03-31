using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Signals;

public class TaxonomyCategory
{
    public Guid Id { get; set; }
    public CivicCategory Category { get; set; }
    public string SubcategorySlug { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
