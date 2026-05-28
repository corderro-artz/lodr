using Bunit;
using Lodr.Components.Steps;
using Lodr.Interop;
using Lodr.Models;
using Lodr.Services;
using Lodr.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lodr.Tests.Components;

/// <summary>
/// Wraps VisualizationInterop so that the DI container can sync-dispose it
/// (VisualizationInterop only implements IAsyncDisposable, which the bUnit
/// service provider cannot handle during synchronous test teardown).
/// </summary>
internal sealed class DisposableVisualizationInterop(IJSRuntime js)
    : VisualizationInterop(js), IDisposable
{
    public void Dispose() { /* async resources released by test teardown via DisposeAsync */ }
}

public class ResultStepTests : BunitContext
{
    private AppState BuildReadyAppState()
    {
        var orient = new OrientationService();
        var calc   = new FitCalculator(orient);
        var engine = new PlacementEngine();
        var state  = new AppState(calc, engine);
        state.SetTrailer(TrailerDimensions.Presets[TrailerType.Standard53]);
        state.SetPallet(PalletSpec.GmaStandard);
        return state;
    }

    private void RegisterServices(AppState appState)
    {
        Services.AddSingleton(appState);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<VisualizationInterop, DisposableVisualizationInterop>();
    }

    [Fact]
    public void ResultStep_OnRender_CalculatesResult()
    {
        var appState = BuildReadyAppState();
        RegisterServices(appState);

        Render<ResultStep>();

        Assert.NotNull(appState.Result);
        Assert.True(appState.Result!.TotalFit > 0);
    }

    [Fact]
    public void ResultStep_ShowsTotalFitValue()
    {
        var appState = BuildReadyAppState();
        RegisterServices(appState);

        var cut = Render<ResultStep>();

        // The total-fit number is rendered in an element with data-total-fit
        var el = cut.Find("[data-total-fit]");
        Assert.Equal(appState.Result!.TotalFit.ToString(), el.TextContent.Trim());
    }
}
