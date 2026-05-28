using Lodr.Models;
using Lodr.Services;
using Microsoft.Extensions.Logging;

namespace Lodr.State;

public class WorkspaceState(
    SlotService slotService,
    ILogger<WorkspaceState> logger)
{
    public event Action? OnChange;

    public Workspace? Current { get; private set; }
    public string? SelectedTrailerId { get; private set; }
    public List<string> OpenTabIds { get; } = [];
    public bool IsDirty { get; private set; }

    public void LoadWorkspace(Workspace ws)
    {
        Current = ws;
        IsDirty = false;
        Notify();
    }

    public void NewBlankWorkspace()
    {
        Current = new Workspace();
        IsDirty = false;
        Notify();
    }

    public void AddTrailer(TrailerDimensions dims, string? friendlyName = null)
    {
        if (Current is null) return;
        int n = Current.Trailers.Count + 1;
        Current.Trailers.Add(new TrailerInstance
        {
            Id = $"T-{n:D2}",
            FriendlyName = friendlyName,
            Dimensions = dims
        });
        Touch();
    }

    public void RemoveTrailer(string trailerId)
    {
        if (Current is null) return;
        Current.Trailers.RemoveAll(t => t.Id == trailerId);
        OpenTabIds.Remove(trailerId);
        if (SelectedTrailerId == trailerId) SelectedTrailerId = null;
        Touch();
    }

    public void RenameTrailer(string trailerId, string friendlyName)
    {
        var trailer = GetTrailer(trailerId);
        if (trailer is null) return;
        trailer.FriendlyName = friendlyName;
        Touch();
    }

    public void SelectTrailer(string? trailerId)
    {
        SelectedTrailerId = trailerId;
        Notify();
    }

    public void OpenTrailerTab(string trailerId)
    {
        if (!OpenTabIds.Contains(trailerId))
            OpenTabIds.Add(trailerId);
        Notify();
    }

    public void CloseTrailerTab(string trailerId)
    {
        OpenTabIds.Remove(trailerId);
        Notify();
    }

    public void AddPalletType(PalletType type)
    {
        Current?.PalletTypes.Add(type);
        Touch();
    }

    public void RemovePalletType(string typeId)
    {
        Current?.PalletTypes.RemoveAll(t => t.Id == typeId);
        Touch();
    }

    public bool TryAddPallet(string? trailerId, string typeId)
    {
        if (Current is null || trailerId is null) return false;
        var trailer = GetTrailer(trailerId);
        var type = Current.PalletTypes.FirstOrDefault(t => t.Id == typeId);
        if (trailer is null || type is null)
        {
            logger.LogWarning("TryAddPallet: trailer={TrailerId} type={TypeId} not found", trailerId, typeId);
            return false;
        }
        int nextIndex = trailer.PlacedPallets.Count == 0
            ? 1
            : trailer.PlacedPallets.Max(p => p.Index) + 1;
        var slot = slotService.FindNextSlot(
            trailer.Dimensions, type, trailer.PlacedPallets, Current.PalletTypes, nextIndex);
        if (slot is null) return false;
        trailer.PlacedPallets.Add(slot);
        Touch();
        return true;
    }

    public void RemovePallet(string trailerId, int palletIndex)
    {
        var trailer = GetTrailer(trailerId);
        if (trailer is null) return;
        trailer.PlacedPallets.RemoveAll(p => p.Index == palletIndex);
        Repack(trailer);
        Touch();
    }

    public bool TryMovePallet(string fromTrailerId, int palletIndex, string toTrailerId)
    {
        if (Current is null) return false;
        var from = GetTrailer(fromTrailerId);
        var to = GetTrailer(toTrailerId);
        if (from is null || to is null) return false;
        var pallet = from.PlacedPallets.FirstOrDefault(p => p.Index == palletIndex);
        if (pallet is null) return false;
        var type = Current.PalletTypes.FirstOrDefault(t => t.Id == pallet.TypeId);
        if (type is null) return false;
        int nextIndex = to.PlacedPallets.Count == 0 ? 1 : to.PlacedPallets.Max(p => p.Index) + 1;
        var slot = slotService.FindNextSlot(
            to.Dimensions, type, to.PlacedPallets, Current.PalletTypes, nextIndex);
        if (slot is null) return false;
        from.PlacedPallets.RemoveAll(p => p.Index == palletIndex);
        Repack(from);
        to.PlacedPallets.Add(slot);
        Touch();
        return true;
    }

    private void Repack(TrailerInstance trailer)
    {
        if (Current is null) return;
        var ordered = trailer.PlacedPallets.OrderBy(p => p.Index).ToList();
        trailer.PlacedPallets.Clear();
        int nextIdx = 1;
        foreach (var p in ordered)
        {
            var type = Current.PalletTypes.FirstOrDefault(t => t.Id == p.TypeId);
            if (type is null) continue;
            var slot = slotService.FindNextSlot(
                trailer.Dimensions, type, trailer.PlacedPallets, Current.PalletTypes, nextIdx++);
            if (slot is not null)
                trailer.PlacedPallets.Add(slot);
        }
    }

    public void SwapPalletType(string trailerId, int palletIndex, string newTypeId)
    {
        var trailer = GetTrailer(trailerId);
        if (trailer is null) return;
        int idx = trailer.PlacedPallets.FindIndex(p => p.Index == palletIndex);
        if (idx < 0) return;
        var old = trailer.PlacedPallets[idx];
        trailer.PlacedPallets[idx] = old with { TypeId = newTypeId };
        Touch();
    }

    private TrailerInstance? GetTrailer(string id) =>
        Current?.Trailers.FirstOrDefault(t => t.Id == id);

    private void Touch()
    {
        if (Current is not null) Current.UpdatedAt = DateTime.UtcNow;
        IsDirty = true;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
