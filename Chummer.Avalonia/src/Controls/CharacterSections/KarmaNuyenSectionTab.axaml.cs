using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Core;
using ScottPlot;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class KarmaNuyenSectionTab : UserControl
{
    public KarmaNuyenSectionTab()
    {
        InitializeComponent();
    }

    public void LoadCharacter(CharacterDocument character)
    {
        var karmaListBox = this.FindControl<ListBox>("KarmaExpensesListBox")!;
        karmaListBox.Items.Clear();
        foreach (CharacterExpenseData expense in character.KarmaExpenses)
            karmaListBox.Items.Add(CreateExpenseRow(expense));

        var nuyenListBox = this.FindControl<ListBox>("NuyenExpensesListBox")!;
        nuyenListBox.Items.Clear();
        foreach (CharacterExpenseData expense in character.NuyenExpenses)
            nuyenListBox.Items.Add(CreateExpenseRow(expense));

        SetUpCareerCharts(character);
    }

    private static ListBoxItem CreateExpenseRow(CharacterExpenseData expense)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("80,60,*") };
        grid.Children.Add(new TextBlock { Text = expense.DisplayDate, [Grid.ColumnProperty] = 0 });
        grid.Children.Add(new TextBlock { Text = expense.Amount, [Grid.ColumnProperty] = 1 });
        grid.Children.Add(new TextBlock { Text = expense.Reason, [Grid.ColumnProperty] = 2 });
        return new ListBoxItem { Content = grid };
    }

    // ScottPlot.Avalonia replacement for the WinForms DataVisualization area charts (Linux port
    // plan Phase 2) - Plot configuration isn't XAML-bindable, so it's built here instead. Running
    // totals are the actual cumulative sum of the character's karma/nuyen expense history, in
    // save order (oldest first) same as the adjacent history list boxes.
    private void SetUpCareerCharts(CharacterDocument character)
    {
        var karmaChart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("KarmaChart")!;
        karmaChart.Plot.Clear();
        SetUpAreaChart(karmaChart.Plot, BuildRunningTotal(character.KarmaExpenses), Colors.RoyalBlue);
        karmaChart.Refresh();

        var nuyenChart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("NuyenChart")!;
        nuyenChart.Plot.Clear();
        SetUpAreaChart(nuyenChart.Plot, BuildRunningTotal(character.NuyenExpenses), Colors.SeaGreen);
        nuyenChart.Refresh();
    }

    private static double[] BuildRunningTotal(System.Collections.Generic.IReadOnlyList<CharacterExpenseData> lstExpenses)
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

    private static void SetUpAreaChart(Plot plot, double[] ys, Color color)
    {
        if (ys.Length < 2) return;
        double[] xs = new double[ys.Length];
        for (int i = 0; i < xs.Length; i++) xs[i] = i;

        var line = plot.Add.Scatter(xs, ys);
        line.LineWidth = 2;
        line.Color = color;
        line.FillY = true;
        line.FillYValue = 0;

        plot.Axes.Bottom.IsVisible = false;
        plot.Axes.Left.IsVisible = false;
        plot.HideGrid();
        plot.Layout.Frameless();
    }
}
