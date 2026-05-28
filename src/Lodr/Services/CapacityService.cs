using Lodr.Models;

namespace Lodr.Services;

public enum FullState { Normal, NearFull, Full }

public class CapacityService(OrientationService orientationService, SlotService slotService)
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

    // Area-based fraction: sq-in of placed pallets / trailer floor area.
    // Used for the visual progress bar — honest for mixed loads.
    public float GetAreaFraction(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        float trailerArea = trailer.Dimensions.Length * trailer.Dimensions.Width;
        if (trailerArea <= 0f || !trailer.PlacedPallets.Any()) return 0f;
        var typeMap = types.ToDictionary(t => t.Id);
        float placedArea = trailer.PlacedPallets.Sum(p =>
        {
            if (!typeMap.TryGetValue(p.TypeId, out var t)) return 0f;
            float l = p.Rotated ? t.Width : t.Length;
            float w = p.Rotated ? t.Length : t.Width;
            return l * w;
        });
        return placedArea / trailerArea;
    }

    // Spatial full: true when FindNextSlot returns null for every defined pallet type.
    // This is the ground truth — no more pallets can physically fit.
    public bool IsSpatiallyFull(TrailerInstance trailer, IReadOnlyList<PalletType> types)
    {
        if (!types.Any() || trailer.Dimensions.Length <= 0) return false;
        int nextIdx = trailer.PlacedPallets.Count == 0
            ? 1 : trailer.PlacedPallets.Max(p => p.Index) + 1;
        foreach (var type in types)
        {
            if (slotService.FindNextSlot(
                    trailer.Dimensions, type, trailer.PlacedPallets, types, nextIdx) is not null)
                return false;
        }
        return true;
    }

    public int CountRemainingSlots(TrailerInstance trailer, PalletType type, IReadOnlyList<PalletType> allTypes)
    {
        var tempPallets = trailer.PlacedPallets.ToList();
        int count = 0;
        int nextIdx = tempPallets.Count == 0 ? 1 : tempPallets.Max(p => p.Index) + 1;
        while (count < 500)
        {
            var slot = slotService.FindNextSlot(trailer.Dimensions, type, tempPallets, allTypes, nextIdx++);
            if (slot is null) break;
            tempPallets.Add(slot);
            count++;
        }
        return count;
    }

    public FullState GetFullState(TrailerInstance trailer, IEnumerable<PalletType> types)
    {
        var typeList = types.ToList();
        if (typeList.Count == 0) return FullState.Normal;
        if (IsSpatiallyFull(trailer, typeList)) return FullState.Full;
        float area = GetAreaFraction(trailer, typeList);
        return area >= 0.8f ? FullState.NearFull : FullState.Normal;
    }
}
