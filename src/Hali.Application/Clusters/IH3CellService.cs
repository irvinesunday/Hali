namespace Hali.Application.Clusters;

public interface IH3CellService
{
	string LatLngToCell(double latitudeDegrees, double longitudeDegrees, int resolution);
	string[] GetKRingCells(string h3CellId, int k);
}
