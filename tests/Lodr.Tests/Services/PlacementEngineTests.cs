using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class PlacementEngineTests
{
    private readonly PlacementEngine _sut = new();

    private static PlacementResult MakeFitResult(TrailerDimensions trailer, PalletSpec pallet, int count) =>
        new([], count, trailer, pallet);

    [Fact]
    public void Generate_2x2Grid_ReturnsCorrectPositions()
    {
        // 96/48=2 cols, 80/40=2 rows
        var result = MakeFitResult(
            new TrailerDimensions(96f, 80f, 110f),
            new PalletSpec(48f, 40f, 48f, CanRotate: false),
            count: 4);

        var positions = _sut.Generate(result);

        Assert.Equal(4, positions.Count);
        Assert.Contains(positions, p => p.X == 0f   && p.Y == 0f);
        Assert.Contains(positions, p => p.X == 48f  && p.Y == 0f);
        Assert.Contains(positions, p => p.X == 0f   && p.Y == 40f);
        Assert.Contains(positions, p => p.X == 48f  && p.Y == 40f);
    }

    [Fact]
    public void Generate_ZeroFit_ReturnsEmptyList()
    {
        var result = MakeFitResult(
            new TrailerDimensions(20f, 20f, 110f),
            new PalletSpec(48f, 40f, 48f, CanRotate: false),
            count: 0);

        var positions = _sut.Generate(result);

        Assert.Empty(positions);
    }

    [Fact]
    public void Generate_IndexesAreSequential()
    {
        // 144/48=3 cols, 1 row → indexes 0,1,2
        var result = MakeFitResult(
            new TrailerDimensions(144f, 40f, 110f),
            new PalletSpec(48f, 40f, 48f, CanRotate: false),
            count: 3);

        var positions = _sut.Generate(result);

        Assert.Equal(new[] { 0, 1, 2 }, positions.Select(p => p.Index).ToArray());
    }

    [Fact]
    public void Generate_AllPositions_HaveRotatedFalse()
    {
        // PlacementEngine uses spec orientation as-is; rotation flag is always false.
        var result = MakeFitResult(
            new TrailerDimensions(96f, 80f, 110f),
            new PalletSpec(48f, 40f, 48f, CanRotate: false),
            count: 4);

        var positions = _sut.Generate(result);

        Assert.All(positions, p => Assert.False(p.Rotated));
    }

    [Fact]
    public void Generate_SingleRow_XPositionsIncrementByPalletLength()
    {
        // 144/48=3 cols
        var result = MakeFitResult(
            new TrailerDimensions(144f, 40f, 110f),
            new PalletSpec(48f, 40f, 48f, CanRotate: false),
            count: 3);

        var positions = _sut.Generate(result).OrderBy(p => p.X).ToList();

        Assert.Equal(0f,   positions[0].X);
        Assert.Equal(48f,  positions[1].X);
        Assert.Equal(96f,  positions[2].X);
    }
}
