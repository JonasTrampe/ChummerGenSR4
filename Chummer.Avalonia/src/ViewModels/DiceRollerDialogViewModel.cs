using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class DiceRollerDialogViewModel : ViewModelBase
{
    private static readonly Random s_random = new();

    public ObservableCollection<string> Methods { get; } = new() { "Standard", "Groß", "Sehr groß" };

    private int _intDiceCount = 1;
    public int DiceCount
    {
        get => _intDiceCount;
        set => SetField(ref _intDiceCount, value < 1 ? 1 : value);
    }

    private string _strSelectedMethod = "Standard";
    public string SelectedMethod
    {
        get => _strSelectedMethod;
        set => SetField(ref _strSelectedMethod, value);
    }

    private int _intGremlins;
    public int Gremlins { get => _intGremlins; set => SetField(ref _intGremlins, value); }

    private int _intThreshold;
    public int Threshold { get => _intThreshold; set => SetField(ref _intThreshold, value); }

    private bool _blnRuleOf6;
    public bool RuleOf6 { get => _blnRuleOf6; set => SetField(ref _blnRuleOf6, value); }

    private bool _blnCinematicGameplay;
    public bool CinematicGameplay { get => _blnCinematicGameplay; set => SetField(ref _blnCinematicGameplay, value); }

    private bool _blnRushedJob;
    public bool RushedJob { get => _blnRushedJob; set => SetField(ref _blnRushedJob, value); }

    private string _strRollsText = string.Empty;
    public string RollsText { get => _strRollsText; set => SetField(ref _strRollsText, value); }

    private string _strResultText = string.Empty;
    public string ResultText { get => _strResultText; set => SetField(ref _strResultText, value); }

    private IBrush _resultColor = Brushes.Black;
    public IBrush ResultColor { get => _resultColor; set => SetField(ref _resultColor, value); }

    public void Roll()
    {
        DiceRollMethod eMethod = SelectedMethod switch
        {
            "Groß" => DiceRollMethod.Large,
            "Sehr groß" => DiceRollMethod.ReallyLarge,
            _ => DiceRollMethod.Standard
        };

        DiceRollResult result = DiceRoller.Roll(DiceCount, eMethod, () => s_random.Next(1, 7),
            Gremlins, RuleOf6, CinematicGameplay, RushedJob, Threshold);

        RollsText = string.Join(" ", result.Rolls);

        if (result.IsCriticalGlitch)
        {
            ResultText = "Patzer! (0 Erfolge)";
            ResultColor = Brushes.DarkRed;
        }
        else if (result.IsGlitch)
        {
            ResultText = "Fehlschlag mit Patzer (" + result.Hits + " Erfolge)";
            ResultColor = Brushes.DarkOrange;
            AppendThresholdResult(result);
        }
        else
        {
            ResultText = "Ergebnis: " + result.Hits + " Erfolge";
            ResultColor = Brushes.Black;
            AppendThresholdResult(result);
        }
    }

    private void AppendThresholdResult(DiceRollResult result)
    {
        if (result.Success == null)
            return;
        ResultText += result.Success == true ? " - Erfolgreich" : " - Fehlgeschlagen";
        ResultColor = result.Success == true ? Brushes.DarkGreen : Brushes.DarkRed;
    }
}
