namespace Lodr.Models;

public record TrailerDimensions(float Length, float Width, float Height)
{
    public static readonly IReadOnlyDictionary<TrailerType, TrailerDimensions> Presets =
        new Dictionary<TrailerType, TrailerDimensions>
        {
            [TrailerType.Standard53] = new(636f, 100f, 110f),
            [TrailerType.Standard48] = new(576f, 100f, 110f),
            [TrailerType.Standard40] = new(480f, 100f, 110f),
            [TrailerType.Standard28] = new(318f, 100f, 110f),
        };

    public static bool ContainsPreset(TrailerType type) => Presets.ContainsKey(type);

    public bool IsValid => Length > 0 && Width > 0 && Height > 0;
}
