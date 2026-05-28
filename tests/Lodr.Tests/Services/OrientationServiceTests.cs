using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class OrientationServiceTests
{
    private readonly OrientationService _sut = new();

    [Fact]
    public void GetOrientations_NoRotate_ReturnsSingleOrientation()
    {
        var spec = new PalletSpec(48f, 40f, 48f, CanRotate: false);
        var result = _sut.GetOrientations(spec);
        Assert.Single(result);
        Assert.Equal((48f, 40f), result[0]);
    }

    [Fact]
    public void GetOrientations_CanRotate_SquarePallet_ReturnsSingleOrientation()
    {
        var spec = new PalletSpec(42f, 42f, 48f, CanRotate: true);
        var result = _sut.GetOrientations(spec);
        Assert.Single(result);
    }

    [Fact]
    public void GetOrientations_CanRotate_ReturnsBothOrientations()
    {
        var spec = new PalletSpec(48f, 40f, 48f, CanRotate: true);
        var result = _sut.GetOrientations(spec);
        Assert.Equal(2, result.Count);
        Assert.Contains((48f, 40f), result);
        Assert.Contains((40f, 48f), result);
    }
}
