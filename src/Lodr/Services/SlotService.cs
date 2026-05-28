using Lodr.Models;

namespace Lodr.Services;

public class SlotService(OrientationService orientationService, CapacityService capacityService)
{
    public PlacedPallet? FindNextSlot(
        TrailerDimensions trailer,
        PalletType type,
        IReadOnlyList<PlacedPallet> existing,
        IReadOnlyList<PalletType> allTypes,
        int nextIndex)
    {
        // Pick best orientation for this type
        var orientations = orientationService.GetOrientations(type);
        (float Length, float Width) bestOrientation = orientations[0];
        int bestFit = 0;
        foreach (var o in orientations)
        {
            int fit = (int)Math.Floor(trailer.Length / o.Length)
                    * (int)Math.Floor(trailer.Width / o.Width);
            if (fit > bestFit) { bestFit = fit; bestOrientation = o; }
        }

        bool rotated = bestOrientation.Length != type.Length;
        float palletL = bestOrientation.Length;
        float palletW = bestOrientation.Width;
        int cols = (int)Math.Floor(trailer.Length / palletL);
        int rows = (int)Math.Floor(trailer.Width / palletW);

        // Build lookup of existing pallet rects using their actual type dims
        var existingRects = existing.Select(p =>
        {
            var t = allTypes.FirstOrDefault(t => t.Id == p.TypeId);
            float l = t?.Length ?? palletL;
            float w = t?.Width ?? palletW;
            if (p.Rotated) (l, w) = (w, l);
            return (p.X, p.Y, l, w);
        }).ToList();

        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            float x = col * palletL;
            float y = row * palletW;
            if (!Overlaps(x, y, palletL, palletW, existingRects))
                return new PlacedPallet(nextIndex, type.Id, x, y, rotated);
        }

        return null;
    }

    private static bool Overlaps(
        float x, float y, float l, float w,
        List<(float X, float Y, float L, float W)> rects)
    {
        foreach (var r in rects)
        {
            bool xOverlap = x < r.X + r.L && x + l > r.X;
            bool yOverlap = y < r.Y + r.W && y + w > r.Y;
            if (xOverlap && yOverlap) return true;
        }
        return false;
    }
}
