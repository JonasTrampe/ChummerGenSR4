using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>Bindable node for a TreeView (via TreeDataTemplate/HierarchicalDataTemplate), replacing
/// the old pattern of building TreeViewItem objects by hand in code-behind. Tracks its own Parent
/// so drag-and-drop reordering/reparenting (see GearSectionTab) can find and mutate the right
/// Children collection without walking the visual tree.</summary>
public sealed class TreeNodeViewModel
{
    public string Name { get; }

    /// <summary>Name to display - the data file's &lt;translate&gt; value if the current language
    /// pack provides one for this item (only wired up for Gear tree nodes so far), otherwise Name.</summary>
    public string TranslatedName { get; private set; }

    public string Category { get; }
    public string Rating { get; }
    public bool Equipped { get; }
    public string SourceName { get; }

    /// <summary>Ballistic/impact armor rating - only set for Armor tree nodes, empty otherwise.</summary>
    public string Ballistic { get; private set; } = string.Empty;

    public string Impact { get; private set; } = string.Empty;
    public string ArmorSetName { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string ItemGuid { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public string CustomName { get; private set; } = string.Empty;
    public string VehicleGuid { get; private set; } = string.Empty;

    /// <summary>Depth-first position within the &lt;gears&gt; tree - only set (>=0) for Gear tree
    /// nodes. Stable identity for adding/removing nested gear and editing quantity.</summary>
    public int GearId { get; private set; } = -1;

    /// <summary>Depth-first position within the &lt;cyberwares&gt; tree - only set (>=0) for
    /// Cyberware/Bioware tree nodes. Stable identity for drag/drop reorder/reparent.</summary>
    public int CyberwareId { get; private set; } = -1;

    /// <summary>Whether this Bioware item was acquired as a custom Transgenic conversion.</summary>
    public bool IsTransgenic { get; private set; }

    /// <summary>Position in the root armor collection, used only to persist armor drag-reordering.</summary>
    public int ArmorId { get; private set; } = -1;
    public int WeaponId { get; private set; } = -1;
    public int QualityId { get; private set; } = -1;
    public int SpellId { get; private set; } = -1;

    public string Qty { get; private set; } = "1";

    /// <summary>Raw saved capacity - only set for Gear tree nodes.</summary>
    public string Capacity { get; private set; } = string.Empty;

    public bool HasCapacity => !string.IsNullOrEmpty(Capacity);

    public string CapacityRemaining { get; private set; } = string.Empty;
    public string CapacityDisplay { get; private set; } = string.Empty;

    /// <summary>Commlink stats - only set (non-empty) for Commlink-category Gear nodes.</summary>
    public string Response { get; private set; } = string.Empty;

    public string Signal { get; private set; } = string.Empty;
    public string System { get; private set; } = string.Empty;
    public string Firewall { get; private set; } = string.Empty;

    /// <summary>Crawled from this node and its descendants (e.g. a Commlink's own Response/Signal
    /// plus its installed Operating System's System/Firewall) - see CharacterTreeItemData.EffectiveStat.</summary>
    public string EffectiveResponse { get; private set; } = string.Empty;

    public string EffectiveSignal { get; private set; } = string.Empty;
    public string EffectiveSystem { get; private set; } = string.Empty;
    public string EffectiveFirewall { get; private set; } = string.Empty;
    public bool HasCommlinkStats { get; private set; }

    /// <summary>Saved vehicle values, populated only for vehicle root nodes.</summary>
    public string Handling { get; private set; } = string.Empty;
    public string Acceleration { get; private set; } = string.Empty;
    public string Speed { get; private set; } = string.Empty;
    public string Pilot { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string VehicleArmor { get; private set; } = string.Empty;
    /// <summary>The saved Sensor value, unless UseCalculatedVehicleSensorRatings is on - see
    /// CharacterVehicleData.SensorDisplay.</summary>
    public string Sensor { get; private set; } = string.Empty;
    public string DeviceRating { get; private set; } = string.Empty;
    public string Avail { get; private set; } = string.Empty;
    public string Cost { get; private set; } = string.Empty;
    public string Slots { get; private set; } = string.Empty;

    /// <summary>Only meaningful for vehicle root nodes - see
    /// CharacterVehicleData.SlotsUsed/TotalSlots/SlotsRemaining. Kept as separate values (not a
    /// pre-formatted string) so the view composes the display text via its own bindings, the same
    /// place every other translatable label in this control lives.</summary>
    public int SlotsUsed { get; private set; }

    public int TotalSlots { get; private set; }
    public int SlotsRemaining { get; private set; }

    /// <summary>Only meaningful for vehicle root nodes - see CharacterVehicleData.TotalCost
    /// (vehicle's own Cost plus installed mods/onboard gear/weapons).</summary>
    public int TotalCost { get; private set; }

    /// <summary>Only set (non-empty) for Weapon root nodes - see
    /// CharacterDocument.ComputeWeaponDicePool.</summary>
    public string WeaponDicePool { get; private set; } = string.Empty;

    public string WeaponDicePoolTooltip { get; private set; } = string.Empty;

    /// <summary>Only set (non-empty) for Weapon root nodes - see
    /// CharacterDocument.ComputeWeaponTotalRc.</summary>
    public string WeaponRc { get; private set; } = string.Empty;
    public string WeaponDamage { get; private set; } = string.Empty;

    /// <summary>Only set (non-empty) for Weapon root nodes with ammo currently loaded - see
    /// CharacterDocument.ReloadWeapon/ComputeAmmoStatus.</summary>
    public string AmmoStatus { get; private set; } = string.Empty;

    public string Source { get; private set; } = string.Empty;
    public string Page { get; private set; } = string.Empty;

    /// <summary>"&lt;book code&gt; &lt;page&gt;" - only meaningful where Source/Page are actually
    /// populated (currently vehicle root nodes), see SourceLink.SourcePage.</summary>
    public string SourcePage => string.IsNullOrWhiteSpace(Page) ? Source : Source + " " + Page;

    public string PhysicalCmFilled { get; private set; } = string.Empty;
    public IReadOnlyList<string> VehicleLocations { get; private set; } = Array.Empty<string>();
    public bool HasVehicleDetails { get; private set; }

    /// <summary>Only set for Spell tree nodes - see CharacterSpellData.</summary>
    public string SpellType { get; private set; } = string.Empty;

    public string SpellRange { get; private set; } = string.Empty;
    public string SpellDamage { get; private set; } = string.Empty;
    public string SpellDuration { get; private set; } = string.Empty;
    public string SpellDv { get; private set; } = string.Empty;
    public string SpellDicePool { get; private set; } = string.Empty;
    public string SpellDicePoolTooltip { get; private set; } = string.Empty;

    public void SetSpellDetails(CharacterSpellData spell)
    {
        SpellType = spell.Type;
        SpellRange = spell.Range;
        SpellDamage = spell.Damage;
        SpellDuration = spell.Duration;
        SpellDv = spell.Dv;
        SpellDicePool = spell.DicePool;
        SpellDicePoolTooltip = spell.DicePoolTooltip;
        Source = spell.Source;
        Page = spell.Page;
    }

    /// <summary>True for a root weapon's Accessory/Mod children - see
    /// CharacterTreeItemData.IsWeaponAccessory/IsWeaponMod.</summary>
    public bool IsWeaponAccessory { get; private set; }

    public bool IsWeaponMod { get; private set; }
    public bool IsWeaponPart => IsWeaponAccessory || IsWeaponMod;
    public bool IncludedInWeapon { get; private set; }

    /// <summary>Name, with the ballistic/impact rating appended for Armor tree nodes, or the
    /// quantity appended for Gear tree nodes with more than one.</summary>
    public string DisplayName
    {
        get
        {
            string strName = string.IsNullOrWhiteSpace(CustomName) ? TranslatedName : TranslatedName + " (\"" + CustomName + "\")";
            if (!string.IsNullOrEmpty(Ballistic) || !string.IsNullOrEmpty(Impact))
                return strName + " (B " + Ballistic + " / I " + Impact + ")";
            if (GearId >= 0 && Qty != "1")
                return strName + " x" + Qty;
            return strName;
        }
    }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
    public bool IsExpanded { get; set; }

    /// <summary>Null for a root-level node (its collection lives directly on the owning
    /// section ViewModel instead of another node's Children).</summary>
    public TreeNodeViewModel? Parent { get; set; }

    public TreeNodeViewModel(string strName, bool blnExpanded = false, string strCategory = "",
        string strRating = "0", bool blnEquipped = false, string strSourceName = "",
        int intQualityId = -1, string strNotes = "", int intSpellId = -1)
    {
        Name = strName;
        TranslatedName = strName;
        IsExpanded = blnExpanded;
        Category = strCategory;
        Rating = strRating;
        Equipped = blnEquipped;
        SourceName = strSourceName;
        QualityId = intQualityId;
        Notes = strNotes;
        SpellId = intSpellId;
    }

    public void AddChild(TreeNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public static TreeNodeViewModel FromTreeItem(CharacterTreeItemData item)
    {
        var node = new TreeNodeViewModel(item.Name, item.Children.Count > 0, item.Category, item.Rating,
            item.Equipped, item.Name)
        {
            TranslatedName = item.TranslatedName,
            Ballistic = item.Ballistic,
            Impact = item.Impact,
            ArmorSetName = item.ArmorSetName,
            Location = item.Location,
            ItemGuid = item.ItemGuid,
            Notes = item.Notes,
            CustomName = item.CustomName,
            GearId = item.GearId,
            CyberwareId = item.CyberwareId,
            ArmorId = item.ArmorId,
            WeaponId = item.WeaponId,
            Qty = item.Qty,
            Capacity = item.Capacity,
            CapacityRemaining = item.CapacityRemaining,
            CapacityDisplay = item.CapacityDisplay,
            Response = item.Response,
            Signal = item.Signal,
            System = item.System,
            Firewall = item.Firewall,
            EffectiveResponse = item.EffectiveResponse,
            EffectiveSignal = item.EffectiveSignal,
            EffectiveSystem = item.EffectiveSystem,
            EffectiveFirewall = item.EffectiveFirewall,
            HasCommlinkStats = item.HasCommlinkStats,
            // Rating-formula cost/avail, evaluated (CalculatedCost includes children, e.g. a
            // Commlink plus its installed Operating System).
            Cost = string.IsNullOrEmpty(item.Cost) ? string.Empty : item.CalculatedCost.ToString(),
            Avail = item.CalculatedAvail,
            WeaponDicePool = item.WeaponDicePool,
            WeaponDicePoolTooltip = item.WeaponDicePoolTooltip,
            WeaponRc = item.WeaponRc,
            WeaponDamage = item.WeaponDamage,
            AmmoStatus = item.AmmoStatus,
            IsWeaponAccessory = item.IsWeaponAccessory,
            IsWeaponMod = item.IsWeaponMod,
            IncludedInWeapon = item.IncludedInWeapon,
            IsTransgenic = item.IsTransgenic
        };
        foreach (CharacterTreeItemData child in item.Children)
            node.AddChild(FromTreeItem(child));
        return node;
    }

    public static TreeNodeViewModel FromVehicle(CharacterVehicleData item)
    {
        var node = new TreeNodeViewModel(item.Name, item.Children.Count > 0, item.Category)
        {
            VehicleGuid = item.Guid,
            Handling = item.Handling,
            Acceleration = item.Acceleration,
            Speed = item.Speed,
            Pilot = item.Pilot,
            Body = item.Body,
            VehicleArmor = item.Armor,
            Sensor = item.SensorDisplay,
            DeviceRating = item.DeviceRating,
            Avail = item.Avail,
            Cost = item.Cost,
            Slots = item.Slots,
            SlotsUsed = item.SlotsUsed,
            TotalSlots = item.TotalSlots,
            SlotsRemaining = item.SlotsRemaining,
            TotalCost = item.TotalCost,
            Source = item.Source,
            Page = item.Page,
            PhysicalCmFilled = item.PhysicalCmFilled,
            VehicleLocations = item.Locations,
            Notes = item.Notes,
            HasVehicleDetails = true
        };
        foreach (CharacterTreeItemData child in item.Children)
            node.AddChild(FromTreeItem(child));
        return node;
    }
}
