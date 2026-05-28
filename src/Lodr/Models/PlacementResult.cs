namespace Lodr.Models;

public record PlacementResult(
    IReadOnlyList<PalletPosition> Positions,
    int TotalFit,
    TrailerDimensions Trailer,
    PalletSpec Pallet
)
{
    public static PlacementResult Empty(TrailerDimensions trailer, PalletSpec pallet) =>
        new([], 0, trailer, pallet);
}
