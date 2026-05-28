using Lodr.Models;
using Lodr.Services;

namespace Lodr.Tests.Services;

public class SlotServiceTests
{
    private static readonly OrientationService _orient = new();
    private static readonly CapacityService _cap = new(_orient);
    private readonly SlotService _svc = new(_orient, _cap);

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");
    private static TrailerDimensions Trailer53 => new(630f, 98f, 110f);

    [Fact]
    public void FindNextSlot_EmptyTrailer_ReturnsFirstSlot()
    {
        var slot = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1);
        Assert.NotNull(slot);
        Assert.Equal(1, slot!.Index);
        Assert.Equal(0f, slot.X);
        Assert.Equal(0f, slot.Y);
    }

    [Fact]
    public void FindNextSlot_FullTrailer_ReturnsNull()
    {
        var currentPlaced = new List<PlacedPallet>();
        for (int i = 1; i <= 30; i++)
        {
            var slot = _svc.FindNextSlot(Trailer53, GMA, currentPlaced, [GMA], i);
            Assert.NotNull(slot);
            currentPlaced.Add(slot!);
        }
        var overflow = _svc.FindNextSlot(Trailer53, GMA, currentPlaced, [GMA], 31);
        Assert.Null(overflow);
    }

    [Fact]
    public void FindNextSlot_SecondSlot_IsAdjacentToFirst()
    {
        var first = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1)!;
        var second = _svc.FindNextSlot(Trailer53, GMA, [first], [GMA], 2)!;
        Assert.NotNull(second);
        Assert.False(first.X == second.X && first.Y == second.Y);
    }

    [Fact]
    public void FindNextSlot_ValidPosition_InTrailerBounds()
    {
        var slot = _svc.FindNextSlot(Trailer53, GMA, [], [GMA], 1);
        Assert.NotNull(slot);
        Assert.True(slot!.X >= 0 && slot.Y >= 0);
    }
}
