using Lodr.Models;
using Lodr.Services;
using Lodr.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lodr.Tests.State;

public class WorkspaceStateTests
{
    private static WorkspaceState MakeState()
    {
        var orient = new OrientationService();
        var cap = new CapacityService(orient);
        var slot = new SlotService(orient, cap);
        return new WorkspaceState(cap, slot, NullLogger<WorkspaceState>.Instance);
    }

    private static PalletType GMA => new("gma", "GMA", 48f, 40f, 48f, true, "#22c55e");

    [Fact]
    public void NewBlankWorkspace_SetsCurrentWorkspace()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        Assert.NotNull(state.Current);
        Assert.Equal("New Workspace", state.Current!.Name);
    }

    [Fact]
    public void AddTrailer_AppendsTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f), "Morning Run");
        Assert.Single(state.Current!.Trailers);
        Assert.Equal("T-01", state.Current.Trailers[0].Id);
        Assert.Equal("Morning Run", state.Current.Trailers[0].FriendlyName);
    }

    [Fact]
    public void AddTrailer_IdsIncrementCorrectly()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddTrailer(new TrailerDimensions(576f, 98f, 110f));
        Assert.Equal("T-01", state.Current!.Trailers[0].Id);
        Assert.Equal("T-02", state.Current.Trailers[1].Id);
    }

    [Fact]
    public void RemoveTrailer_RemovesCorrectTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddTrailer(new TrailerDimensions(576f, 98f, 110f));
        state.RemoveTrailer("T-01");
        Assert.Single(state.Current!.Trailers);
        Assert.Equal("T-02", state.Current.Trailers[0].Id);
    }

    [Fact]
    public void AddPalletType_AppendsType()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddPalletType(GMA);
        Assert.Single(state.Current!.PalletTypes);
    }

    [Fact]
    public void TryAddPallet_NoTrailerSelected_ReturnsFalse()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        Assert.False(state.TryAddPallet(null, "gma"));
    }

    [Fact]
    public void TryAddPallet_ValidTrailerAndType_AddsPallet()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        var added = state.TryAddPallet("T-01", "gma");
        Assert.True(added);
        Assert.Single(state.Current!.Trailers[0].PlacedPallets);
        Assert.Equal(1, state.Current.Trailers[0].PlacedPallets[0].Index);
    }

    [Fact]
    public void RemovePallet_RemovesCorrectPallet()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.AddPalletType(GMA);
        state.TryAddPallet("T-01", "gma");
        state.TryAddPallet("T-01", "gma");
        state.RemovePallet("T-01", 1);
        Assert.Single(state.Current!.Trailers[0].PlacedPallets);
        Assert.Equal(2, state.Current.Trailers[0].PlacedPallets[0].Index);
    }

    [Fact]
    public void OnChange_FiredOnAddTrailer()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        bool fired = false;
        state.OnChange += () => fired = true;
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        Assert.True(fired);
    }

    [Fact]
    public void OpenTrailerTab_AddsToOpenTabs()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.OpenTrailerTab("T-01");
        Assert.Contains("T-01", state.OpenTabIds);
    }

    [Fact]
    public void CloseTrailerTab_RemovesFromOpenTabs()
    {
        var state = MakeState();
        state.NewBlankWorkspace();
        state.AddTrailer(new TrailerDimensions(630f, 98f, 110f));
        state.OpenTrailerTab("T-01");
        state.CloseTrailerTab("T-01");
        Assert.DoesNotContain("T-01", state.OpenTabIds);
    }
}
