using Lodr.Models;

namespace Lodr.Services;

public class OrientationService
{
    public IReadOnlyList<(float Length, float Width)> GetOrientations(PalletSpec spec)
    {
        var original = (spec.Length, spec.Width);
        if (!spec.CanRotate || spec.Length == spec.Width)
            return [original];

        return [original, (spec.Width, spec.Length)];
    }
}
