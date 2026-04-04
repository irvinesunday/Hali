using System.Collections.Generic;

namespace Hali.Contracts.Home;

public record PagedSection<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public string? NextCursor { get; init; }
    public int TotalCount { get; init; }
}
