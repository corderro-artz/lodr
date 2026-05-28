using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class CapacityServiceTests
{
    private readonly CapacityService _svc = new(new OrientationService());

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");
    private static TrailerDimensions Trailer53 => new(630f, 98f, 110f);

    [Fact]
    public void GetCapacity_GmaIn53_Returns30()
    {
        // rotated: floor(630/40) * floor(98/48) = 15 * 2 = 30
        var result = _svc.GetCapacity(Trailer53, GMA);
        Assert.Equal(30, result);
    }

    [Fact]
    public void GetCapacity_NoRotate_UsesOriginalOnly()
    {
        var noRotate = new PalletType("x", "X", 48f, 40f, 48f, false, "#fff");
        // floor(630/48) * floor(98/40) = 13 * 2 = 26
        var result = _svc.GetCapacity(Trailer53, noRotate);
        Assert.Equal(26, result);
    }

    [Fact]
    public void GetUsedFraction_ZeroPlaced_ReturnsZero()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        var types = new List<PalletType> { GMA };
        Assert.Equal(0f, _svc.GetUsedFraction(trailer, types));
    }

    [Fact]
    public void GetUsedFraction_HalfFull_Returns0Point5()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        // capacity = 30, place 15
        for (int i = 0; i < 15; i++)
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        var fraction = _svc.GetUsedFraction(trailer, types);
        Assert.Equal(0.5f, fraction, precision: 4);
    }

    [Fact]
    public void GetFullState_Under80_IsNormal()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.Normal, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetFullState_At80Percent_IsNearFull()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        for (int i = 0; i < 24; i++) // 24/30 = 0.8
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.NearFull, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetFullState_At100Percent_IsFull()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        for (int i = 0; i < 30; i++)
            trailer.PlacedPallets.Add(new PlacedPallet(i + 1, "gma", 0, 0, false));
        var types = new List<PalletType> { GMA };
        Assert.Equal(FullState.Full, _svc.GetFullState(trailer, types));
    }

    [Fact]
    public void GetCapacity_EmptyTypes_ReturnsZeroFraction()
    {
        var trailer = new TrailerInstance { Id = "T-01", Dimensions = Trailer53 };
        Assert.Equal(0f, _svc.GetUsedFraction(trailer, []));
    }
}
