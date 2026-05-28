using Lodr.Models;

namespace Lodr.Services;

public class OrientationService
{
    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletSpec spec)
        => GetOrientationsCore(spec.Length, spec.Width, spec.CanRotate);

    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletType type)
        => GetOrientationsCore(type.Length, type.Width, type.CanRotate);

    private static IReadOnlyList<(float Length, float Width)> GetOrientationsCore(
        float length, float width, bool canRotate)
    {
        var original = (length, width);
        if (!canRotate || length == width) return [original];
        return [original, (width, length)];
    }
}
