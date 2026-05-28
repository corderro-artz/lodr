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
/// Wraps PersistenceInterop so that the DI container can sync-dispose it
/// (PersistenceInterop only implements IAsyncDisposable, which the bUnit
/// service provider cannot handle during synchronous test teardown).
/// </summary>
internal sealed class DisposablePersistenceInterop(IJSRuntime js)
    : PersistenceInterop(js), IDisposable
{
    public void Dispose() { /* async resources released by test teardown via DisposeAsync */ }
}

public class TrailerStepTests : BunitContext
{
    private AppState BuildAppState()
    {
        var orient = new OrientationService();
        var calc   = new FitCalculator(orient);
        var engine = new PlacementEngine();
        return new AppState(calc, engine);
    }

    private void RegisterServices(AppState appState)
    {
        Services.AddSingleton(appState);
        // Silence JS interop calls from PersistenceInterop
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Register as the base PersistenceInterop type so @inject resolves it,
        // but use the disposable wrapper so sync DI teardown doesn't throw.
        Services.AddSingleton<PersistenceInterop, DisposablePersistenceInterop>();
    }

    [Fact]
    public void TrailerStep_SelectStandard53_SetsTrailerOnAppState()
    {
        var appState = BuildAppState();
        RegisterServices(appState);

        var cut = Render<TrailerStep>();

        cut.Find("select[data-trailer-type]").Change("0"); // TrailerType.Standard53 = 0

        Assert.NotNull(appState.Trailer);
        Assert.Equal(630f, appState.Trailer!.Length);
        Assert.Equal(98f,  appState.Trailer.Width);
    }

    [Fact]
    public void TrailerStep_CustomDimensions_SetsTrailerOnAppState()
    {
        var appState = BuildAppState();
        RegisterServices(appState);

        var cut = Render<TrailerStep>();

        // Switch to Custom mode
        cut.Find("select[data-trailer-type]").Change("99"); // TrailerType.Custom = 99

        cut.Find("input[data-dim='length']").Change("500");
        cut.Find("input[data-dim='width']").Change("96");
        cut.Find("input[data-dim='height']").Change("108");

        Assert.NotNull(appState.Trailer);
        Assert.Equal(500f, appState.Trailer!.Length);
        Assert.Equal(96f,  appState.Trailer.Width);
        Assert.Equal(108f, appState.Trailer.Height);
    }
}
