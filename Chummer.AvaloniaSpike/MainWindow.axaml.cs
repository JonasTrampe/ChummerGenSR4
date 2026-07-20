using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Chummer.AvaloniaSpike.Controls;
using Chummer.AvaloniaSpike.Dialogs;
using ScottPlot;

namespace Chummer.AvaloniaSpike;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Tracks the item and button a drag session started with - DoDragDropAsync only needs an
    // IDataTransfer to satisfy the OS-level drag session, the actual "what to move"/"how"
    // bookkeeping is simpler kept here directly rather than round-tripped through a
    // DataTransferItem.
    private TreeViewItem? _draggedGearItem;
    private bool _draggedWithRightButton;

    // Press-then-threshold state: DoDragDropAsync requires the original PointerPressedEventArgs,
    // but the drag itself must only actually engage once the pointer has moved a few pixels -
    // otherwise every plain click (including on the expand/collapse box) immediately hijacks
    // itself into an OS-level drag session before the click can be processed normally.
    private PointerPressedEventArgs? _pendingPressArgs;
    private TreeViewItem? _pendingPressItem;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;
    private readonly CharacterFileService _characterFiles = new CharacterFileService();
    private CharacterDocument? _loadedCharacter;
    private OpenCharacterTab? _selectedOpenCharacter;

    public ObservableCollection<OpenCharacterTab> OpenCharacters { get; } = new();

    public OpenCharacterTab? SelectedOpenCharacter
    {
        get => _selectedOpenCharacter;
        set
        {
            if (ReferenceEquals(_selectedOpenCharacter, value))
                return;

            _selectedOpenCharacter = value;
            OnPropertyChanged();
            if (value is not null)
                ActivateCharacter(value.Character);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DataContext = this;
        try
        {
            Title = "Chummer - [" + App.LanguageCatalog.GetString("Title_CareerMode") + " (Default Settings)]";
        }
        catch
        {
            // The spike remains runnable when language resources are absent during design-time use.
        }
        SetUpCareerCharts();
        SetUpGearDragDrop();
    }

    // Avalonia DragDrop prototype for the gear-reordering risk area flagged in the Linux port
    // plan's Avalonia audit (frmCareer/frmCreate gear/cyberware lists). Matches the real app's two
    // distinct gestures: left-button drag reorders siblings within the same level, right-button
    // drag reparents the dragged item to become a child of the drop target - which covers both
    // "embed into" (dropping on some other item) and "extract out of" (dropping on an ancestor,
    // moving the item back up a level) with the same single rule.
    private void SetUpGearDragDrop()
    {
        var tree = this.FindControl<TreeView>("AusruestungTree")!;

        tree.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var pointerProperties = e.GetCurrentPoint(tree).Properties;
            var isRightButton = pointerProperties.IsRightButtonPressed;
            if ((!pointerProperties.IsLeftButtonPressed && !isRightButton)
                || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) is not null
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } source)
                return;

            // Just remember the candidate - do NOT start the OS-level drag yet. Engaging it
            // immediately on every press (before any actual movement) is what broke plain clicks:
            // it hijacked the gesture before the expand/collapse ToggleButton, item selection, etc.
            // ever got a chance to see it.
            _pendingPressArgs = e;
            _pendingPressItem = source;
            _draggedWithRightButton = isRightButton;
            _pendingPressPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (_pendingPressArgs is not { } pressArgs || _pendingPressItem is not { } source)
                return;

            var currentPoint = e.GetPosition(tree);
            var dx = currentPoint.X - _pendingPressPoint.X;
            var dy = currentPoint.Y - _pendingPressPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            _pendingPressArgs = null;
            _pendingPressItem = null;
            _draggedGearItem = source;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, source.Header?.ToString() ?? "gear");
            var transfer = new DataTransfer();
            transfer.Add(item);
            try
            {
                // A drag session can fail for all sorts of reasons at the OS/COM level (dropped
                // outside any valid target, input state changing mid-drag, etc.) - none of that is
                // fatal, it just means no move happens, so it must never be allowed to crash the app.
                await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move);
            }
            catch
            {
                // Swallow deliberately - see comment above.
            }
            finally
            {
                _draggedGearItem = null;
            }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            _pendingPressArgs = null;
            _pendingPressItem = null;
        }, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(tree, true);
        tree.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Move;
            // Avalonia's Win32 drop target only honors DragEffects/actually allows a drop when the
            // event is marked Handled - without this the OS side falls back to treating the whole
            // gesture as rejected, so the Drop event below never fires at all (no crash, just a
            // silent no-op, which is exactly what "does nothing" looked like).
            e.Handled = true;
        });
        tree.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;

            // e.Source isn't reliable here - unlike pointer events, Avalonia's drag routing
            // doesn't consistently set it to the actual element under the cursor, so it's hit-
            // tested explicitly at the drop coordinates instead.
            if (_draggedGearItem is not { } source
                || FindTreeViewItemAt(tree, e.GetPosition(tree)) is not { } target
                || source == target
                || source.Parent is not ItemsControl sourceParent)
                return;

            // Deliberately typed as ItemCollection, not IList: ItemCollection's own Remove(object)
            // (which actually mutates the collection) has a different signature (returns bool) than
            // IList.Remove(object) (returns void), so it does NOT satisfy that interface member -
            // the interface slot instead falls back to the base ItemsSourceView's IList.Remove,
            // which unconditionally throws NotSupportedException. Calling through an IList-typed
            // reference silently ate that exception (Avalonia's Win32 drop-target COM shim swallows
            // exceptions from managed handlers), which is exactly why nothing ever moved.
            var sourceSiblings = sourceParent.Items;

            if (_draggedWithRightButton)
            {
                // Reparent: refuse to drop an item onto itself or into its own subtree, since that
                // would either no-op or orphan the whole branch.
                if (IsAncestorOf(source, target) || target is not ItemsControl targetParent)
                    return;

                sourceSiblings.Remove(source);
                targetParent.Items.Add(source);
            }
            else
            {
                if (source.Parent != target.Parent)
                    return;

                var targetIndex = sourceSiblings.IndexOf(target);
                sourceSiblings.Remove(source);
                sourceSiblings.Insert(targetIndex, source);
            }
        });
    }

    private static TreeViewItem? FindTreeViewItemAt(IInputElement tree, Point pointRelativeToTree)
        => (tree.InputHitTest(pointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true);

    private static bool IsAncestorOf(TreeViewItem candidate, TreeViewItem item)
    {
        for (var node = item.Parent; node is not null; node = (node as Control)?.Parent)
        {
            if (ReferenceEquals(node, candidate))
                return true;
        }

        return false;
    }

    // ScottPlot.Avalonia replacement for the WinForms DataVisualization area charts (Linux port
    // plan Phase 2) - Plot configuration isn't XAML-bindable, so it's built here instead. Sample
    // running totals match the karma/nuyen history entries already shown in the adjacent list
    // boxes, just extended with a few earlier points so the area chart has a believable shape.
    private void SetUpCareerCharts()
    {
        double[] days = { 0, 5, 10, 15, 20, 25, 30 };

        var karmaChart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("KarmaChart")!;
        double[] karmaRunningTotal = { 0, 5, 5, 1, 1, 18, 18 };
        SetUpAreaChart(karmaChart.Plot, days, karmaRunningTotal, Colors.RoyalBlue);

        var nuyenChart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("NuyenChart")!;
        double[] nuyenRunningTotal = { 2560, 2560, 2440, 1440, 1435, 1435, 1435 };
        SetUpAreaChart(nuyenChart.Plot, days, nuyenRunningTotal, Colors.SeaGreen);
    }

    private static void SetUpAreaChart(Plot plot, double[] xs, double[] ys, Color color)
    {
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

    // Demo wiring only, for the look-and-feel spike: chains the three real character-creation
    // dialogs (settings profile -> GP count -> metatype) the same way the real app's "Neu"
    // toolbar button does.
    private async void OnNewCharacterClick(object? sender, RoutedEventArgs e)
    {
        var settingsDialog = new SettingsProfileDialog();
        await settingsDialog.ShowDialog(this);

        var gpDialog = new KarmaGpDialog();
        await gpDialog.ShowDialog(this);

        var metatypeDialog = new MetatypeDialog();
        await metatypeDialog.ShowDialog(this);
    }

    private async void OnOpenCharacterClick(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Chummer character",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Chummer characters") { Patterns = new[] { "*.chum", "*.xml" } },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
        {
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                CharacterDocument character = _characterFiles.Load(stream, files[0].Name);
                var tab = new OpenCharacterTab(character);
                OpenCharacters.Add(tab);
                SelectedOpenCharacter = tab;
            }
            catch (Exception)
            {
                // CharacterFileService already recorded the load failure; leave the active tab alone.
            }
        }
    }

    private void ActivateCharacter(CharacterDocument character)
    {
        _loadedCharacter = character;
        UpdateCharacterStatus(character);
        UpdateCharacterOverview(character);
        UpdateCharacterTrees(character);
        Title = "Chummer - " + character.Name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void UpdateCharacterStatus(CharacterDocument character)
    {
        this.FindControl<TextBlock>("KarmaStatus")!.Text = "Karma: " + character.Karma;
        this.FindControl<TextBlock>("NuyenStatus")!.Text = "Nuyen: " + character.Nuyen + "¥";
    }

    private void UpdateCharacterOverview(CharacterDocument character)
    {
        this.FindControl<TextBox>("AliasTextBox")!.Text = character.Alias;
        this.FindControl<TextBlock>("MetatypeText")!.Text = character.Metatype;
        this.FindControl<TextBox>("NuyenTextBox")!.Text = character.Nuyen;
        this.FindControl<TextBlock>("NuyenEquivalentText")!.Text = "= " + character.Nuyen + "¥";

        foreach (CharacterAttributeData attribute in character.Attributes)
        {
            var row = this.FindControl<AttributeRow>(attribute.Code + "Attribute");
            if (row is null)
                continue;

            row.Base = attribute.Value;
            row.Augmented = attribute.TotalValue == attribute.Value ? string.Empty : "(" + attribute.TotalValue + ")";
            row.Range = attribute.Minimum + " / " + attribute.Maximum + " (" + attribute.AugmentedMaximum + ")";
        }
    }

    private void UpdateCharacterTrees(CharacterDocument character)
    {
        var qualitiesTree = this.FindControl<TreeView>("QualitiesTree")!;
        qualitiesTree.Items.Clear();
        var positiveQualities = new TreeViewItem { Header = "Positive qualities", IsExpanded = true };
        var negativeQualities = new TreeViewItem { Header = "Negative qualities", IsExpanded = true };
        foreach (CharacterQualityData quality in character.Qualities)
        {
            var parent = quality.Type == "Negative" ? negativeQualities : positiveQualities;
            parent.Items.Add(new TreeViewItem { Header = quality.DisplayName });
        }
        if (positiveQualities.ItemCount > 0) qualitiesTree.Items.Add(positiveQualities);
        if (negativeQualities.ItemCount > 0) qualitiesTree.Items.Add(negativeQualities);

        var gearTree = this.FindControl<TreeView>("AusruestungTree")!;
        gearTree.Items.Clear();
        foreach (CharacterTreeItemData item in character.Gear)
            gearTree.Items.Add(CreateTreeViewItem(item));

        var weaponsTree = this.FindControl<TreeView>("WeaponsTree")!;
        weaponsTree.Items.Clear();
        foreach (CharacterWeaponData weapon in character.Weapons)
            weaponsTree.Items.Add(new TreeViewItem { Header = weapon.DisplayName });
    }

    private static TreeViewItem CreateTreeViewItem(CharacterTreeItemData item)
    {
        var treeItem = new TreeViewItem { Header = item.Name, IsExpanded = item.Children.Count > 0 };
        foreach (CharacterTreeItemData child in item.Children)
            treeItem.Items.Add(CreateTreeViewItem(child));
        return treeItem;
    }

    private async void OnSaveCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (_loadedCharacter is null)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Chummer character",
            DefaultExtension = "chum",
            SuggestedFileName = _loadedCharacter.Name,
            FileTypeChoices = new[] { new FilePickerFileType("Chummer characters") { Patterns = new[] { "*.chum" } } },
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            _characterFiles.Save(_loadedCharacter, stream, file.Name);
        }
    }

    private async void OnAddQualityClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new QualityDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SpellDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SheetPreviewDialog();
        await dialog.ShowDialog(this);
    }
}
