using Lodr.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Lodr.Interop;

public class WorkspacePersistenceInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/indexeddb-interop.js");
        return _module;
    }

    public async Task SaveWorkspaceAsync(Workspace ws)
    {
        ws.UpdatedAt = DateTime.UtcNow;
        var m = await GetModuleAsync();
        var obj = JsonSerializer.SerializeToElement(ws, _json);
        await m.InvokeVoidAsync("saveItem", "workspaces", obj);
    }

    public async Task<Workspace?> GetWorkspaceAsync(string id)
    {
        var m = await GetModuleAsync();
        var result = await m.InvokeAsync<JsonElement?>("getItem", "workspaces", id);
        if (result is null) return null;
        return JsonSerializer.Deserialize<Workspace>(result.Value.GetRawText(), _json);
    }

    public async Task<List<Workspace>> GetAllWorkspacesAsync()
    {
        var m = await GetModuleAsync();
        var result = await m.InvokeAsync<JsonElement>("getAllItems", "workspaces");
        if (result.ValueKind == JsonValueKind.Null) return [];
        return result.EnumerateArray()
            .Select(e => JsonSerializer.Deserialize<Workspace>(e.GetRawText(), _json))
            .Where(w => w is not null)
            .Select(w => w!)
            .ToList();
    }

    public async Task DeleteWorkspaceAsync(string id)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("deleteItem", "workspaces", id);
    }

    public async Task SavePresetAsync(WorkspacePreset preset)
    {
        var m = await GetModuleAsync();
        var obj = JsonSerializer.SerializeToElement(preset, _json);
        await m.InvokeVoidAsync("saveItem", "workspace-presets", obj);
    }

    public async Task<List<WorkspacePreset>> GetAllPresetsAsync()
    {
        var m = await GetModuleAsync();
        var result = await m.InvokeAsync<JsonElement>("getAllItems", "workspace-presets");
        if (result.ValueKind == JsonValueKind.Null) return [];
        return result.EnumerateArray()
            .Select(e => JsonSerializer.Deserialize<WorkspacePreset>(e.GetRawText(), _json))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    public async Task SaveLastWorkspaceIdAsync(string id)
    {
        var m = await GetModuleAsync();
        await m.InvokeVoidAsync("saveItem", "last-workspace-id",
            new { id = "last", workspaceId = id });
    }

    public async Task<string?> GetLastWorkspaceIdAsync()
    {
        var m = await GetModuleAsync();
        var result = await m.InvokeAsync<JsonElement?>("getItem", "last-workspace-id", "last");
        if (result is null) return null;
        return result.Value.TryGetProperty("workspaceId", out var prop) ? prop.GetString() : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
