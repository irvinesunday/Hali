namespace Hali.Application.Clusters;

public interface IH3CellService
{
	string[] GetKRingCells(string h3CellId, int k);
}
