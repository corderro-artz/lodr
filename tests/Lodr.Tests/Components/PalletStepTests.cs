using Bunit;
using Lodr.Components.Steps;
using Lodr.Interop;
using Lodr.Models;
using Lodr.Services;
using Lodr.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Lodr.Tests.Components;

public class PalletStepTests : BunitContext
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
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<PersistenceInterop, DisposablePersistenceInterop>();
    }

    [Fact]
    public void PalletStep_DefaultValues_SetsGmaStandardOnAppState()
    {
        var appState = BuildAppState();
        RegisterServices(appState);

        Render<PalletStep>();

        // Component sets GMA defaults on init
        Assert.NotNull(appState.Pallet);
        Assert.Equal(48f, appState.Pallet!.Length);
        Assert.Equal(40f, appState.Pallet.Width);
    }

    [Fact]
    public void PalletStep_ChangeLength_UpdatesAppState()
    {
        var appState = BuildAppState();
        RegisterServices(appState);

        var cut = Render<PalletStep>();
        cut.Find("input[data-dim='length']").Change("36");

        Assert.Equal(36f, appState.Pallet!.Length);
    }
}
