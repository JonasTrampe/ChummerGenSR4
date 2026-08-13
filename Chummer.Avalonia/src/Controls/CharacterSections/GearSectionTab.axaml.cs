using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System.IO;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using ArmorDialog = Chummer.NewUI.Dialogs.ArmorDialog;
using ArmorModDialog = Chummer.NewUI.Dialogs.ArmorModDialog;
using GearDialog = Chummer.NewUI.Dialogs.GearDialog;
using NexusDialog = Chummer.NewUI.Dialogs.NexusDialog;
using WeaponDialog = Chummer.NewUI.Dialogs.WeaponDialog;
using WeaponAccessoryDialog = Chummer.NewUI.Dialogs.WeaponAccessoryDialog;
using WeaponModDialog = Chummer.NewUI.Dialogs.WeaponModDialog;
using LifestyleDialog = Chummer.NewUI.Dialogs.LifestyleDialog;
using AdvancedLifestyleDialog = Chummer.NewUI.Dialogs.AdvancedLifestyleDialog;
using ArmorSetDialog = Chummer.NewUI.Dialogs.ArmorSetDialog;
using SellItemDialog = Chummer.NewUI.Dialogs.SellItemDialog;
using NaturalWeaponDialog = Chummer.NewUI.Dialogs.NaturalWeaponDialog;
using ReloadDialog = Chummer.NewUI.Dialogs.ReloadDialog;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.Controls.CharacterSections;

public partial class GearSectionTab : UserControl
{
    // Tracks the item and button a drag session started with - DoDragDropAsync only needs an
    // IDataTransfer to satisfy the OS-level drag session, the actual "what to move"/"how"
    // bookkeeping is simpler kept here directly rather than round-tripped through a
    // DataTransferItem. These track the bound ViewModel node, not the visual TreeViewItem - the
    // tree is data-bound now, so drag/drop mutates the ViewModel's TreeNodeViewModel graph and
    // lets the bindings redraw it, rather than reaching into the visual tree's Items collection.
    private TreeNodeViewModel? _draggedGearNode;
    private bool _draggedWithRightButton;

    // Press-then-threshold state: DoDragDropAsync requires the original PointerPressedEventArgs,
    // but the drag itself must only actually engage once the pointer has moved a few pixels -
    // otherwise every plain click (including on the expand/collapse box) immediately hijacks
    // itself into an OS-level drag session before the click can be processed normally.
    private PointerPressedEventArgs? _pendingPressArgs;
    private TreeNodeViewModel? _pendingPressNode;
    private Point _pendingPressPoint;
    private const double DragThreshold = 6;

    private CharacterDocument? _character;

    public GearSectionViewModel ViewModel { get; } = new();

    public GearSectionTab()
    {
        DataContext = ViewModel;
        InitializeComponent();
        SetUpGearDragDrop();
        SetUpRootTreeReorderDragDrop(this.FindControl<TreeView>("WeaponsTree")!, MoveWeapon);
        SetUpRootTreeReorderDragDrop(this.FindControl<TreeView>("ArmorTree")!, MoveArmor);
    }

    public void LoadCharacter(CharacterDocument character)
    {
        _character = character;
        ViewModel.LoadCharacter(character);
    }

    private async void OnAddLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new LifestyleDialog(_character);
        if (await dialog.ShowDialog<bool>(window) && dialog.SelectedLifestyle != null)
        {
            var lifestyle = dialog.SelectedLifestyle;
            _character.AddLifestyle(lifestyle.Name, lifestyle.Cost, "1");
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddAdvancedLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new AdvancedLifestyleDialog(_character);
        if (await dialog.ShowDialog<bool>(window))
        {
            _character.AddAdvancedLifestyle(dialog.LifestyleName, dialog.Comforts, dialog.Entertainment, dialog.Necessities,
                dialog.Neighborhood, dialog.Security, dialog.Roommates, dialog.Percentage, dialog.PositiveQualities,
                dialog.NegativeQualities);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteLifestyleClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedLifestyle == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteLifestyle"))
            return;
        if (_character.RemoveLifestyle(ViewModel.SelectedLifestyle.Name)) ViewModel.LoadCharacter(_character);
    }

    private async void OnAddGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        bool continueAdding;
        do
        {
            var dialog = new GearDialog(_character);
            bool added = await dialog.ShowDialog<bool>(window);
            if (added && dialog.SelectedGear != null)
            {
                var gear = dialog.SelectedGear;
                _character.AddGear(gear.SourceName, gear.Category, gear.Rating, gear.Quantity.ToString(),
                    gear.Cost, gear.Availability, gear.SourcePage, string.Empty,
                    gear.Capacity, gear.Response, gear.Signal, gear.SystemRating, gear.Firewall);
                // Ignoring the return value here is intentional: a rejected Stick-n-Shock pickup
                // is a rare, self-explanatory (no owned eligible weapon) house-rule edge case, not
                // worth a dedicated error dialog for - matches this port's existing convention of
                // silently no-op'ing rejected adds elsewhere (e.g. weapon accessory mounts).
            }
            continueAdding = added && dialog.ContinueAdding;
        } while (continueAdding);

        ViewModel.LoadCharacter(_character);
    }

    private async void OnAddNexusClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new NexusDialog();
        if (await dialog.ShowDialog<bool>(window))
        {
            _character.AddNexus(dialog.Processor, dialog.Response, dialog.System, dialog.Firewall,
                dialog.Signal, dialog.Persona, dialog.Free);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddGearLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new ArmorSetDialog { Title = App.LanguageCatalog.GetString("UI_AddGearLocationTitle") };
        if (await dialog.ShowDialog<bool>(window) && _character.AddGearLocation(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRemoveGearLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { Category: "Gear location" } location
            || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteGearLocation"))
            return;
        if (_character.RemoveGearLocation(location.Name))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Adds gear nested under the currently selected gear item (e.g. a Certified
    /// Credstick under a Commlink) instead of at the root of the Ausrüstung tree.</summary>
    private async void OnAddChildGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedGear is not { GearId: >= 0 } parent)
            return;

        bool continueAdding;
        do
        {
            var dialog = new GearDialog(_character);
            bool added = await dialog.ShowDialog<bool>(window);
            if (added && dialog.SelectedGear != null)
            {
                var gear = dialog.SelectedGear;
                _character.AddChildGear(parent.GearId, gear.SourceName, gear.Category, gear.Rating, gear.Quantity.ToString(),
                    gear.Cost, gear.Availability, gear.SourcePage, string.Empty,
                    gear.Capacity, gear.Response, gear.Signal, gear.SystemRating, gear.Firewall);
            }
            continueAdding = added && dialog.ContinueAdding;
        } while (continueAdding);

        ViewModel.LoadCharacter(_character);
    }

    private async void OnDeleteGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteGear"))
            return;
        if (_character.RemoveGear(node.GearId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnEditGearNotesClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ContactNotesDialog { Notes = node.Notes };
        if (await dialog.ShowDialog<bool>(window) && _character.SetGearNotes(node.GearId, dialog.Notes))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellGearClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellGear(node.GearId, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleGearEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedGear is not { GearId: >= 0 } node || sender is not CheckBox checkBox)
            return;

        if (_character.SetGearEquipped(node.GearId, checkBox.IsChecked == true))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new WeaponDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedWeapon != null)
        {
            var weapon = dialog.SelectedWeapon;
            _character.AddWeapon(weapon.Name, weapon.Category, weapon.Damage, weapon.Ap, weapon.Mode, weapon.Rc,
                weapon.Ammo, weapon.Cost, weapon.Availability, weapon.SourcePage, string.Empty);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteWeapon"))
            return;

        if (selected.Parent == null)
        {
            if (_character.RemoveWeapon(selected.SourceName, selected.Category))
                ViewModel.LoadCharacter(_character);
            return;
        }

        if ((selected.IsWeaponAccessory || selected.IsWeaponMod) && selected.Parent is { } weapon
            && Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId) && Guid.TryParse(selected.ItemGuid, out Guid guiChildId))
        {
            bool removed = selected.IsWeaponAccessory
                ? _character.RemoveWeaponAccessory(guiWeaponId, guiChildId)
                : _character.RemoveWeaponMod(guiWeaponId, guiChildId);
            if (removed)
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnAddNaturalWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new NaturalWeaponDialog(_character);
        if (await dialog.ShowDialog<bool>(window)
            && _character.AddNaturalWeapon(dialog.ResultName, dialog.ResultSkill, dialog.ResultDvBase,
                dialog.ResultDvMod, dialog.ResultDvType, dialog.ResultAp, dialog.ResultReach))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnReloadWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } selected
            || !Guid.TryParse(selected.ItemGuid, out Guid guiWeaponId)
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ReloadDialog(_character, guiWeaponId);
        if (await dialog.ShowDialog<bool>(window)
            && _character.ReloadWeapon(guiWeaponId, dialog.SelectedAmmoGearId, dialog.SelectedCount))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellWeaponClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } selected
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellWeapon(selected.SourceName, selected.Category, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Resolves the root weapon a "Zubehör/Mod hinzufügen" click applies to: the selected
    /// node itself if it's already a weapon (root-level, or nested under a Weapon location), or its
    /// direct parent if an accessory/mod/gear/ammo child is selected instead.</summary>
    private static TreeNodeViewModel? ResolveWeaponNode(TreeNodeViewModel selected)
    {
        if (selected.Category == "Weapon location")
            return null;
        return selected.Parent == null || selected.Parent.Category == "Weapon location" ? selected : selected.Parent;
    }

    private async void OnAddWeaponAccessoryClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedWeapon is not { } selected || ResolveWeaponNode(selected) is not { } weapon
            || !Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId))
            return;

        var dialog = new WeaponAccessoryDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedAccessory is not { } accessory) return;
        if (_character.AddWeaponAccessory(guiWeaponId, accessory.Name, accessory.Mount, accessory.Rc,
                accessory.Availability, accessory.Cost, accessory.Source, accessory.Page, accessory.RcGroup))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddWeaponModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window
            || ViewModel.SelectedWeapon is not { } selected || ResolveWeaponNode(selected) is not { } weapon
            || !Guid.TryParse(weapon.ItemGuid, out Guid guiWeaponId))
            return;

        var dialog = new WeaponModDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (!added || dialog.SelectedMod is not { } mod) return;
        if (_character.AddWeaponMod(guiWeaponId, mod.Name, dialog.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                mod.Slots, mod.Availability, mod.Cost, mod.Source, mod.Page, mod.Rc, mod.RcGroup))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddWeaponLocationClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window) return;
        var dialog = new ArmorSetDialog { Title = App.LanguageCatalog.GetString("UI_AddWeaponLocationTitle") };
        if (await dialog.ShowDialog<bool>(window) && _character.AddWeaponLocation(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleWeaponEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedWeapon is not { Parent: null } weapon || sender is not CheckBox checkBox)
            return;
        if (_character.SetWeaponEquipped(weapon.SourceName, weapon.Category, checkBox.IsChecked == true))
            ViewModel.LoadCharacter(_character);
    }

    private void OnToggleWeaponPartIncludedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || sender is not CheckBox { IsChecked: { } blnIncluded }
            || ViewModel.SelectedWeapon is not { IsWeaponPart: true, Parent: { } parent } part
            || !Guid.TryParse(parent.ItemGuid, out Guid guiWeaponId)
            || !Guid.TryParse(part.ItemGuid, out Guid guiPartId))
            return;

        if (_character.SetWeaponPartIncluded(guiWeaponId, guiPartId, blnIncluded))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new ArmorDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedArmor != null)
        {
            var armor = dialog.SelectedArmor;
            _character.AddArmor(armor.Name, armor.Category, armor.Ballistic, armor.Impact, armor.Capacity,
                armor.Cost, armor.Availability, armor.SourcePage, string.Empty);
            ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (ViewModel.SelectedArmor.Category == "Armor set")
        {
            if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmorLocation"))
                return;
            if (_character.RemoveArmorSet(ViewModel.SelectedArmor.Name)) ViewModel.LoadCharacter(_character);
            return;
        }
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmor"))
            return;
        if (_character.RemoveArmor(ViewModel.SelectedArmor.SourceName, ViewModel.SelectedArmor.Category))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnSellArmorClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { } selected || selected.Category == "Armor set"
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        var dialog = new SellItemDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.SellArmor(selected.SourceName, selected.Category, dialog.SellPercent))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Adds an Armor Modification under whichever Armor is selected - if the selection is
    /// itself one of that Armor's own children (a previously-added mod/gear), walks up to the
    /// parent Armor first, same as how legacy always operates on the owning Armor regardless of
    /// which of its rows is focused.</summary>
    private async void OnAddArmorModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;

        TreeNodeViewModel? armorNode = ViewModel.SelectedArmor;
        while (armorNode?.Parent is { Category: not "Armor set" })
            armorNode = armorNode.Parent;
        if (armorNode == null || armorNode.Category == "Armor set")
            return;

        var dialog = new ArmorModDialog(_character);
        bool added = await dialog.ShowDialog<bool>(window);
        if (added && dialog.SelectedMod != null)
        {
            var mod = dialog.SelectedMod;
            if (_character.AddArmorMod(armorNode.SourceName, armorNode.Category, mod.Name,
                    dialog.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    mod.Ballistic, mod.Impact, mod.Availability, mod.Cost, mod.Source, mod.Page))
                ViewModel.LoadCharacter(_character);
        }
    }

    private async void OnDeleteArmorModClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Parent.Category: not "Armor set" } mod
            || TopLevel.GetTopLevel(this) is not Window window)
            return;

        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmor"))
            return;
        if (_character.RemoveArmorMod(mod.SourceName))
            ViewModel.LoadCharacter(_character);
    }

    private void OnArmorBallisticDamageClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(1, 0);
    private void OnArmorBallisticRepairClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(-1, 0);
    private void OnArmorImpactDamageClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(0, 1);
    private void OnArmorImpactRepairClick(object? sender, RoutedEventArgs e) => AdjustSelectedArmorDegradation(0, -1);

    private void AdjustSelectedArmorDegradation(int intBallisticDelta, int intImpactDelta)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Category: not "Armor set" } armor)
            return;
        if (_character.AdjustArmorDegradation(armor.SourceName, armor.Category, intBallisticDelta, intImpactDelta))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnAddArmorSetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        var dialog = new ArmorSetDialog();
        if (await dialog.ShowDialog<bool>(window) && _character.AddArmorSet(dialog.SetName))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnRemoveArmorSetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor is not { Category: "Armor set" } armorSet
            || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteArmorLocation"))
            return;
        if (_character.RemoveArmorSet(armorSet.Name)) ViewModel.LoadCharacter(_character);
    }

    private void OnAddPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null)
            return;
        _character.AddPet("Neues Haustier");
        ViewModel.LoadCharacter(_character);
    }

    private async void OnDeletePetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet == null || TopLevel.GetTopLevel(this) is not Window window)
            return;
        if (!await DeleteConfirmation.ConfirmAsync(window, _character, "Message_DeleteContact"))
            return;
        if (_character.RemoveContact(ViewModel.SelectedPet.ContactId))
            ViewModel.LoadCharacter(_character);
    }

    private async void OnLinkPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet == null || TopLevel.GetTopLevel(this) is not { StorageProvider: { } storage })
            return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = App.LanguageCatalog.GetString("UI_SelectCompanionCharacterTitle"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Chummer character") { Patterns = ["*.chum"] }]
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;
        _character.UpdateContactFile(ViewModel.SelectedPet.ContactId, path,
            Path.GetRelativePath(AppContext.BaseDirectory, path));
        ViewModel.LoadCharacter(_character);
    }

    /// <summary>Clears a Pet's linked companion file without deleting the Pet itself - matches
    /// PetControl.cs's tsRemoveCharacter context-menu entry.</summary>
    private void OnUnlinkPetClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedPet is not { HasLinkedCharacter: true } selected)
            return;
        if (_character.UpdateContactFile(selected.ContactId, string.Empty, string.Empty))
            ViewModel.LoadCharacter(_character);
    }

    /// <summary>Opens a Pet's linked companion file in a new tab - matches PetControl.cs's
    /// tsContactOpen context-menu entry.</summary>
    private void OnOpenLinkedPetCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPet is not { HasLinkedCharacter: true } selected
            || !File.Exists(selected.FileName)
            || TopLevel.GetTopLevel(this) is not MainWindow window)
            return;

        using var stream = File.OpenRead(selected.FileName);
        CharacterDocument linked = new CharacterFileService().Load(stream, Path.GetFileName(selected.FileName));
        window.LoadCharacterIntoTabs(linked, selected.FileName);
    }

    private void OnToggleArmorEquippedClick(object? sender, RoutedEventArgs e)
    {
        if (_character == null || ViewModel.SelectedArmor == null || sender is not CheckBox checkBox)
            return;

        bool blnEquipped = checkBox.IsChecked == true;
        if (_character.SetArmorEquipped(ViewModel.SelectedArmor.SourceName, ViewModel.SelectedArmor.Category, blnEquipped))
            ViewModel.LoadCharacter(_character);
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
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } sourceItem
                || sourceItem.DataContext is not TreeNodeViewModel sourceNode)
                return;

            // Just remember the candidate - do NOT start the OS-level drag yet. Engaging it
            // immediately on every press (before any actual movement) is what broke plain clicks:
            // it hijacked the gesture before the expand/collapse ToggleButton, item selection, etc.
            // ever got a chance to see it.
            _pendingPressArgs = e;
            _pendingPressNode = sourceNode;
            _draggedWithRightButton = isRightButton;
            _pendingPressPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (_pendingPressArgs is not { } pressArgs || _pendingPressNode is not { } sourceNode)
                return;

            var currentPoint = e.GetPosition(tree);
            var dx = currentPoint.X - _pendingPressPoint.X;
            var dy = currentPoint.Y - _pendingPressPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            _pendingPressArgs = null;
            _pendingPressNode = null;
            _draggedGearNode = sourceNode;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, sourceNode.Name);
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
                _draggedGearNode = null;
            }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            _pendingPressArgs = null;
            _pendingPressNode = null;
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
            if (_character == null
                || _draggedGearNode is not { } source
                || FindTreeNodeAt(tree, e.GetPosition(tree)) is not { } target
                || source == target
                || source.GearId < 0 || target.GearId < 0)
                return;

            if (_draggedWithRightButton)
            {
                // Reparent: refuse to drop an item onto itself or into its own subtree, since that
                // would either no-op or orphan the whole branch (MoveGear also guards against this).
                if (IsAncestorOf(source, target))
                    return;

                if (_character.MoveGear(source.GearId, target.GearId, blnReparent: true))
                    ViewModel.LoadCharacter(_character);
            }
            else
            {
                if (source.Parent != target.Parent)
                    return;

                if (_character.MoveGear(source.GearId, target.GearId, blnReparent: false))
                    ViewModel.LoadCharacter(_character);
            }
        });
    }

    private static TreeNodeViewModel? FindTreeNodeAt(IInputElement tree, Point pointRelativeToTree)
        => ((tree.InputHitTest(pointRelativeToTree) as Visual)?.FindAncestorOfType<TreeViewItem>(true))
            ?.DataContext as TreeNodeViewModel;

    private static bool IsAncestorOf(TreeNodeViewModel candidate, TreeNodeViewModel node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, candidate))
                return true;
        }

        return false;
    }

    /// <summary>Sets up left-button reordering for trees whose groups (weapon locations and armor
    /// sets) are presentation-only. Unlike Gear/Cyberware, those XML items cannot be reparented;
    /// drops are accepted only between siblings and persist their underlying root XML order.</summary>
    private void SetUpRootTreeReorderDragDrop(TreeView tree, Func<TreeNodeViewModel, TreeNodeViewModel, bool> move)
    {
        TreeNodeViewModel? pending = null;
        PointerPressedEventArgs? pendingArgs = null;
        Point pendingPoint = default;
        TreeNodeViewModel? dragged = null;

        tree.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            var props = e.GetCurrentPoint(tree).Properties;
            if (!props.IsLeftButtonPressed
                || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) is not null
                || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true) is not { } item
                || item.DataContext is not TreeNodeViewModel node)
                return;
            pending = node;
            pendingArgs = e;
            pendingPoint = e.GetPosition(tree);
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerMovedEvent, async (_, e) =>
        {
            if (pending is not { } source || pendingArgs is not { } pressArgs)
                return;
            Point current = e.GetPosition(tree);
            double dx = current.X - pendingPoint.X;
            double dy = current.Y - pendingPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold)
                return;

            pending = null;
            pendingArgs = null;
            dragged = source;
            var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, source.Name);
            transfer.Add(item);
            try { await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move); }
            catch { /* An abandoned OS drag is a harmless no-op. */ }
            finally { dragged = null; }
        }, RoutingStrategies.Tunnel);

        tree.AddHandler(InputElement.PointerReleasedEvent, (_, _) => { pending = null; pendingArgs = null; }, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(tree, true);
        tree.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DragDropEffects.Move; e.Handled = true; });
        tree.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            if (dragged is not { } source || FindTreeNodeAt(tree, e.GetPosition(tree)) is not { } target
                || source == target || source.Parent != target.Parent)
                return;
            if (move(source, target))
                ViewModel.LoadCharacter(_character!);
        });
    }

    private bool MoveWeapon(TreeNodeViewModel source, TreeNodeViewModel target) => _character != null
        && Guid.TryParse(source.ItemGuid, out Guid sourceId) && Guid.TryParse(target.ItemGuid, out Guid targetId)
        && _character.MoveWeapon(sourceId, targetId);

    private bool MoveArmor(TreeNodeViewModel source, TreeNodeViewModel target) => _character != null
        && source.ArmorId >= 0 && target.ArmorId >= 0 && _character.MoveArmor(source.ArmorId, target.ArmorId);
}
