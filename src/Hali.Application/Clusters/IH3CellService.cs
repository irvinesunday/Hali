namespace Hali.Application.Clusters;

public interface IH3CellService
{
    /// <summary>
    /// Returns the cell itself plus all k-ring neighbors at distance k.
    /// For k=1 this is the cell + its 6 immediate neighbors (7 total).
    /// </summary>
    string[] GetKRingCells(string h3CellId, int k);
}
