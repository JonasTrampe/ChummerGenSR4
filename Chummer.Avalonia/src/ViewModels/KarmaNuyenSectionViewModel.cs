using System.Collections.Generic;
using System.Collections.ObjectModel;
using Chummer.Core;
using ScottPlot;
using ScottPlot.Avalonia;

namespace Chummer.NewUI.ViewModels;

public sealed class ExpenseRowViewModel
{
    public string Date { get; }
    public string Amount { get; }
    public string Reason { get; }

    public ExpenseRowViewModel(CharacterExpenseData expense)
    {
        Date = expense.DisplayDate;
        Amount = expense.Amount;
        Reason = expense.Reason;
    }
}

public sealed class KarmaNuyenSectionViewModel : ViewModelBase
{
    public ObservableCollection<ExpenseRowViewModel> KarmaExpenses { get; } = new();
    public ObservableCollection<ExpenseRowViewModel> NuyenExpenses { get; } = new();

    // ScottPlot's own MVVM pattern (see https://scottplot.net/quickstart/wpf/, same shape for
    // Avalonia): the ViewModel owns the AvaPlot instance directly instead of the View naming it
    // and code-behind reaching in via FindControl - the View just hosts it with a
    // ContentControl/Mode=OneTime binding.
    public AvaPlot KarmaChart { get; } = new();
    public AvaPlot NuyenChart { get; } = new();

    public void LoadCharacter(CharacterDocument character)
    {
        KarmaExpenses.Clear();
        foreach (CharacterExpenseData expense in character.KarmaExpenses)
            KarmaExpenses.Add(new ExpenseRowViewModel(expense));

        NuyenExpenses.Clear();
        foreach (CharacterExpenseData expense in character.NuyenExpenses)
            NuyenExpenses.Add(new ExpenseRowViewModel(expense));

        SetUpAreaChart(KarmaChart, BuildRunningTotal(character.KarmaExpenses), Colors.RoyalBlue);
        SetUpAreaChart(NuyenChart, BuildRunningTotal(character.NuyenExpenses), Colors.SeaGreen);
    }

    // ScottPlot.Avalonia replacement for the WinForms DataVisualization area charts (Linux port
    // plan Phase 2) - Plot configuration isn't XAML-bindable itself, so it's built here instead.
    // Running totals are the actual cumulative sum of the character's karma/nuyen expense
    // history, in save order (oldest first) same as the adjacent history list boxes.
    private static void SetUpAreaChart(AvaPlot avaPlot, double[] ys, Color color)
    {
        avaPlot.Plot.Clear();
        if (ys.Length >= 2)
        {
            double[] xs = new double[ys.Length];
            for (int i = 0; i < xs.Length; i++) xs[i] = i;

            var line = avaPlot.Plot.Add.Scatter(xs, ys);
            line.LineWidth = 2;
            line.Color = color;
            line.FillY = true;
            line.FillYValue = 0;

            avaPlot.Plot.Axes.Bottom.IsVisible = false;
            avaPlot.Plot.Axes.Left.IsVisible = false;
            avaPlot.Plot.HideGrid();
            avaPlot.Plot.Layout.Frameless();
        }

        avaPlot.Refresh();
    }

    private static double[] BuildRunningTotal(IReadOnlyList<CharacterExpenseData> lstExpenses)
    {
        var arrTotals = new double[lstExpenses.Count + 1];
        double dblRunning = 0;
        for (int i = 0; i < lstExpenses.Count; i++)
        {
            if (decimal.TryParse(lstExpenses[i].Amount, out var decAmount))
                dblRunning += (double)decAmount;
            arrTotals[i + 1] = dblRunning;
        }

        return arrTotals;
    }
}
