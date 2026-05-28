using Lodr.Models;
using Microsoft.JSInterop;

namespace Lodr.Interop;

public class VisualizationInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/pixi-interop.js");
        return _module;
    }

    public async Task InitAsync(string canvasId, int width, int height)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("init", canvasId, width, height);
    }

    public async Task RenderAsync(PlacementResult result)
    {
        var m = await GetModuleAsync();
        var positions = result.Positions
            .Select(p => new { x = p.X, y = p.Y, rotated = p.Rotated, index = p.Index })
            .ToArray();
        await m.InvokeVoidAsync("render",
            result.Trailer.Length,
            result.Trailer.Width,
            positions,
            result.Pallet.Length,
            result.Pallet.Width);
    }

    public async Task ClearAsync()
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("clear");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("destroy"); } catch { }
            await _module.DisposeAsync();
        }
    }
}
