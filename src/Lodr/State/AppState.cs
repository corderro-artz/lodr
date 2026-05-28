using Lodr.Models;
using Lodr.Services;

namespace Lodr.State;

public class AppState(FitCalculator fitCalculator, PlacementEngine placementEngine)
{
    public event Action? OnChange;

    public TrailerDimensions? Trailer { get; private set; }
    public PalletSpec? Pallet { get; private set; }
    public PlacementResult? Result { get; private set; }
    public int CurrentStep { get; private set; } = 0;

    public bool CanAdvance => CurrentStep switch
    {
        0 => Trailer is not null && Trailer.IsValid,
        1 => Pallet is not null && Pallet.IsValid,
        _ => false
    };

    public void SetTrailer(TrailerDimensions trailer)
    {
        Trailer = trailer;
        Result = null;
        NotifyStateChanged();
    }

    public void SetPallet(PalletSpec pallet)
    {
        Pallet = pallet;
        Result = null;
        NotifyStateChanged();
    }

    public void Calculate()
    {
        if (Trailer is null || Pallet is null) return;
        var fitResult = fitCalculator.Calculate(Trailer, Pallet);
        var positions = placementEngine.Generate(fitResult);
        Result = fitResult with { Positions = positions };
        NotifyStateChanged();
    }

    public void NextStep()
    {
        if (!CanAdvance) return;
        CurrentStep = Math.Min(CurrentStep + 1, 2);
        NotifyStateChanged();
    }

    public void PrevStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            NotifyStateChanged();
        }
    }

    public void GoToStep(int step)
    {
        if (step >= 0 && step <= 2)
        {
            CurrentStep = step;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
