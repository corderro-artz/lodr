using Lodr.Models;

namespace Lodr.Services;

public class PlacementEngine
{
    public IReadOnlyList<PalletPosition> Generate(PlacementResult fitResult)
    {
        if (fitResult.TotalFit == 0)
            return [];

        var pallet = fitResult.Pallet;
        var trailer = fitResult.Trailer;
        int cols = (int)Math.Floor(trailer.Length / pallet.Length);

        var positions = new List<PalletPosition>(fitResult.TotalFit);
        int index = 0;
        int row = 0;

        while (positions.Count < fitResult.TotalFit)
        {
            for (int col = 0; col < cols && positions.Count < fitResult.TotalFit; col++)
            {
                positions.Add(new PalletPosition(
                    X: col * pallet.Length,
                    Y: row * pallet.Width,
                    Rotated: false,
                    Index: index++
                ));
            }
            row++;
        }

        return positions;
    }
}
