using Lodr.Models;

namespace Lodr.Services;

public enum FullState { Normal, NearFull, Full }

public class CapacityService(OrientationService orientationService)
{
    public int GetCapacity(TrailerDimensions trailer, PalletType type)
    {
        var orientations = orientationService.GetOrientations(type);
        int best = 0;
        foreach (var (length, width) in orientations)
        {
            int fit = (int)Math.Floor(trailer.Length / length)
                    * (int)Math.Floor(trailer.Width / width);
            if (fit > best) best = fit;
        }
        return best;
    }

    public float GetUsedFraction(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        var typeList = types.ToList();
        if (typeList.Count == 0) return 0f;
        int maxCap = typeList.Max(t => GetCapacity(trailer.Dimensions, t));
        if (maxCap == 0) return 0f;
        return (float)trailer.PlacedPallets.Count / maxCap;
    }

    public FullState GetFullState(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        float fraction = GetUsedFraction(trailer, types);
        return fraction >= 1f ? FullState.Full
             : fraction >= 0.8f ? FullState.NearFull
             : FullState.Normal;
    }
}
