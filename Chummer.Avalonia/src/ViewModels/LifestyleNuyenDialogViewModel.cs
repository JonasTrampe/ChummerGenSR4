using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Backs LifestyleNuyenDialog - ported from frmLifestyleNuyen.cs's live formula preview
/// (lblResult), which updates as the player's manually-entered dice-roll result changes.</summary>
public sealed class LifestyleNuyenDialogViewModel : ViewModelBase
{
    private readonly CharacterDocument.LifestyleNuyenRollInfo _info;

    public LifestyleNuyenDialogViewModel(CharacterDocument.LifestyleNuyenRollInfo info)
    {
        _info = info;
        MinResult = info.Dice;
        MaxResult = info.Dice * 6;
        DiceResult = MinResult;
    }

    public decimal MinResult { get; }
    public decimal MaxResult { get; }

    public string Instructions =>
        $"Wirf {_info.Dice}W6 für dein Startgeld gemäß deinem Lifestyle und trage das Ergebnis ein.";

    private decimal _decDiceResult;
    public decimal DiceResult
    {
        get => _decDiceResult;
        set
        {
            if (SetField(ref _decDiceResult, value))
                OnPropertyChanged(nameof(FormulaPreview));
        }
    }

    public string FormulaPreview =>
        $"({DiceResult} + {_info.Extra}) x {_info.Multiplier} = {(DiceResult + _info.Extra) * _info.Multiplier:###,###,##0}¥";
}
