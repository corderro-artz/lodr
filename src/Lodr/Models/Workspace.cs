namespace Lodr.Models;

public class Workspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Workspace";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<TrailerInstance> Trailers { get; set; } = [];
    public List<PalletType> PalletTypes { get; set; } = [];
}

public class TrailerInstance
{
    public string Id { get; set; } = string.Empty;        // "T-01", "T-02"
    public string? FriendlyName { get; set; }
    public TrailerDimensions Dimensions { get; set; } = new(0, 0, 0);
    public List<PlacedPallet> PlacedPallets { get; set; } = [];
}

public record PalletType(
    string Id,
    string Name,
    float Length,
    float Width,
    float Height,
    bool CanRotate,
    string Color
);

public record PlacedPallet(
    int Index,
    string TypeId,
    float X,
    float Y,
    bool Rotated
);

public class WorkspacePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<PresetTrailer> DefaultTrailers { get; set; } = [];
    public List<PalletType> PalletTypes { get; set; } = [];
}

public record PresetTrailer(string Label, TrailerDimensions Dimensions);
