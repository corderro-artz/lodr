using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class FitCalculatorTests
{
    private readonly FitCalculator _sut = new(new OrientationService());

    [Fact]
    public void Calculate_RotationHelps_PicksBestOrientation()
    {
        // Original 48×40: 480/48=10 cols × 98/40=2 rows = 20
        // Rotated  40×48: 480/40=12 cols × 98/48=2 rows = 24  ← best
        var trailer = new TrailerDimensions(480f, 98f, 110f);
        var pallet = new PalletSpec(48f, 40f, 48f, CanRotate: true);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Equal(24, result.TotalFit);
    }

    [Fact]
    public void Calculate_NoRotate_UsesOriginalOrientation()
    {
        // 480/48=10 × 98/40=2 = 20
        var trailer = new TrailerDimensions(480f, 98f, 110f);
        var pallet = new PalletSpec(48f, 40f, 48f, CanRotate: false);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Equal(20, result.TotalFit);
    }

    [Fact]
    public void Calculate_PalletLargerThanTrailer_ReturnsZero()
    {
        var trailer = new TrailerDimensions(40f, 30f, 110f);
        var pallet = new PalletSpec(48f, 40f, 48f, CanRotate: false);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Equal(0, result.TotalFit);
    }

    [Fact]
    public void Calculate_ExactFit_ReturnsCorrectCount()
    {
        // 96/48=2 cols × 80/40=2 rows = 4
        var trailer = new TrailerDimensions(96f, 80f, 110f);
        var pallet = new PalletSpec(48f, 40f, 48f, CanRotate: false);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Equal(4, result.TotalFit);
    }

    [Fact]
    public void Calculate_RotationWorsens_KeepsOriginal()
    {
        // Original 60×40: 100/60=1 col  × 48/40=1 row  = 1
        // Rotated  40×60: 100/40=2 cols × 48/60=0 rows = 0  ← worse
        var trailer = new TrailerDimensions(100f, 48f, 110f);
        var pallet = new PalletSpec(60f, 40f, 48f, CanRotate: true);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Equal(1, result.TotalFit);
    }

    [Fact]
    public void Calculate_Result_HasEmptyPositions()
    {
        var trailer = new TrailerDimensions(480f, 98f, 110f);
        var pallet = new PalletSpec(48f, 40f, 48f, CanRotate: false);

        var result = _sut.Calculate(trailer, pallet);

        Assert.Empty(result.Positions);
    }
}
