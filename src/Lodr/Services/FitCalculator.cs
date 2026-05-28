using Lodr.Models;

namespace Lodr.Services;

public class FitCalculator(OrientationService orientationService)
{
    public PlacementResult Calculate(TrailerDimensions trailer, PalletSpec spec)
    {
        var orientations = orientationService.GetOrientations(spec);

        (float Length, float Width) bestOrientation = orientations[0];
        int bestFit = 0;

        foreach (var (length, width) in orientations)
        {
            int cols = (int)Math.Floor(trailer.Length / length);
            int rows = (int)Math.Floor(trailer.Width / width);
            int fit = cols * rows;
            if (fit > bestFit)
            {
                bestFit = fit;
                bestOrientation = (length, width);
            }
        }

        return new PlacementResult(
            Positions: [],
            TotalFit: bestFit,
            Trailer: trailer,
            Pallet: spec with { Length = bestOrientation.Length, Width = bestOrientation.Width }
        );
    }
}
