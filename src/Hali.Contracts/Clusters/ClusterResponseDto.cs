namespace Hali.Contracts.Clusters;

public record ClusterResponseDto(
    Guid Id,
    string State,
    string Category,
    string? SubcategorySlug,
    string? Title,
    string? Summary,
    int AffectedCount,
    int ObservingCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ActivatedAt,
    DateTime? PossibleRestorationAt,
    DateTime? ResolvedAt
);
