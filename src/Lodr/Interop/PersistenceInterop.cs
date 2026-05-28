using Lodr.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Lodr.Interop;

public class PersistenceInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private const string TrailerPresetsStore = "trailer-presets";
    private const string PalletPresetsStore = "pallet-presets";
    private const string LastUsedStore = "last-used";

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/indexeddb-interop.js");
        return _module;
    }

    public async Task SaveLastTrailerAsync(TrailerDimensions dims)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("saveItem", LastUsedStore,
            new { id = "trailer", length = dims.Length, width = dims.Width, height = dims.Height });
    }

    public async Task<TrailerDimensions?> GetLastTrailerAsync()
    {
        var m = await GetModuleAsync();
        var item = await m.InvokeAsync<JsonElement?>("getItem", LastUsedStore, "trailer");
        if (item is null) return null;
        var v = item.Value;
        return new TrailerDimensions(
            v.GetProperty("length").GetSingle(),
            v.GetProperty("width").GetSingle(),
            v.GetProperty("height").GetSingle());
    }

    public async Task SaveLastPalletAsync(PalletSpec pallet)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("saveItem", LastUsedStore,
            new { id = "pallet", length = pallet.Length, width = pallet.Width,
                  height = pallet.Height, canRotate = pallet.CanRotate });
    }

    public async Task<PalletSpec?> GetLastPalletAsync()
    {
        var m = await GetModuleAsync();
        var item = await m.InvokeAsync<JsonElement?>("getItem", LastUsedStore, "pallet");
        if (item is null) return null;
        var v = item.Value;
        return new PalletSpec(
            v.GetProperty("length").GetSingle(),
            v.GetProperty("width").GetSingle(),
            v.GetProperty("height").GetSingle(),
            v.GetProperty("canRotate").GetBoolean());
    }

    public async Task SaveTrailerPresetAsync(string id, TrailerDimensions dims)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("saveItem", TrailerPresetsStore,
            new { id, length = dims.Length, width = dims.Width, height = dims.Height });
    }

    public async Task<List<(string Id, TrailerDimensions Dims)>> GetTrailerPresetsAsync()
    {
        var m = await GetModuleAsync();
        var items = await m.InvokeAsync<JsonElement>("getAllItems", TrailerPresetsStore);
        if (items.ValueKind == JsonValueKind.Null) return [];
        var result = new List<(string, TrailerDimensions)>();
        foreach (var item in items.EnumerateArray())
        {
            result.Add((
                item.GetProperty("id").GetString()!,
                new TrailerDimensions(
                    item.GetProperty("length").GetSingle(),
                    item.GetProperty("width").GetSingle(),
                    item.GetProperty("height").GetSingle())
            ));
        }
        return result;
    }

    public async Task SavePalletPresetAsync(string id, PalletSpec pallet)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("saveItem", PalletPresetsStore,
            new { id, length = pallet.Length, width = pallet.Width,
                  height = pallet.Height, canRotate = pallet.CanRotate });
    }

    public async Task<List<(string Id, PalletSpec Spec)>> GetPalletPresetsAsync()
    {
        var m = await GetModuleAsync();
        var items = await m.InvokeAsync<JsonElement>("getAllItems", PalletPresetsStore);
        if (items.ValueKind == JsonValueKind.Null) return [];
        var result = new List<(string, PalletSpec)>();
        foreach (var item in items.EnumerateArray())
        {
            result.Add((
                item.GetProperty("id").GetString()!,
                new PalletSpec(
                    item.GetProperty("length").GetSingle(),
                    item.GetProperty("width").GetSingle(),
                    item.GetProperty("height").GetSingle(),
                    item.GetProperty("canRotate").GetBoolean())
            ));
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
