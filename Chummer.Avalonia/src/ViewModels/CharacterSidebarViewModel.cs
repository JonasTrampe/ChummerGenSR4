using Chummer.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Chummer.NewUI.ViewModels;

public sealed class CharacterSidebarViewModel : ViewModelBase
{
    private CharacterDocument? _objCharacter;
    private bool _blnUpdatingCommlinks;

    private string _strEssence = string.Empty;
    public string Essence { get => _strEssence; set => SetField(ref _strEssence, value); }

    private string _strPhysicalDamage = string.Empty;
    public string PhysicalDamage { get => _strPhysicalDamage; set => SetField(ref _strPhysicalDamage, value); }

    private string _strStunDamage = string.Empty;
    public string StunDamage { get => _strStunDamage; set => SetField(ref _strStunDamage, value); }

    public DerivedValueViewModel PhysicalMonitor { get; } = new();
    public DerivedValueViewModel StunMonitor { get; } = new();
    public DerivedValueViewModel BallisticArmor { get; } = new();
    public DerivedValueViewModel ImpactArmor { get; } = new();
    public DerivedValueViewModel BallisticEncumbrance { get; } = new();
    public DerivedValueViewModel ImpactEncumbrance { get; } = new();
    public DerivedValueViewModel Initiative { get; } = new();
    public DerivedValueViewModel InitiativePasses { get; } = new();
    public DerivedValueViewModel AstralInitiative { get; } = new();
    public DerivedValueViewModel MatrixInitiative { get; } = new();
    public DerivedValueViewModel MatrixInitiativePasses { get; } = new();
    public DerivedValueViewModel Composure { get; } = new();
    public DerivedValueViewModel JudgeIntentions { get; } = new();
    public DerivedValueViewModel LiftAndCarry { get; } = new();
    public DerivedValueViewModel Memory { get; } = new();
    public DerivedValueViewModel DamageResistance { get; } = new();
    public ObservableCollection<ConditionMonitorBoxViewModel> PhysicalMonitorBoxes { get; } = new();
    public ObservableCollection<ConditionMonitorBoxViewModel> StunMonitorBoxes { get; } = new();

    private string _strWoundModifiers = "0";
    public string WoundModifiers { get => _strWoundModifiers; set => SetField(ref _strWoundModifiers, value); }

    private string _strWalkMovement = string.Empty;
    public string WalkMovement { get => _strWalkMovement; set => SetField(ref _strWalkMovement, value); }

    private string _strSwimMovement = string.Empty;
    public string SwimMovement { get => _strSwimMovement; set => SetField(ref _strSwimMovement, value); }

    private string _strFlyMovement = string.Empty;
    public string FlyMovement { get => _strFlyMovement; set => SetField(ref _strFlyMovement, value); }

    private string _strEdge = "0 von 0 verbleibend";
    public string Edge { get => _strEdge; set => SetField(ref _strEdge, value); }

    private string _strRemainingNuyen = string.Empty;
    public string RemainingNuyen { get => _strRemainingNuyen; set => SetField(ref _strRemainingNuyen, value); }

    private string _strCareerKarma = string.Empty;
    public string CareerKarma { get => _strCareerKarma; set => SetField(ref _strCareerKarma, value); }

    private string _strCareerNuyen = string.Empty;
    public string CareerNuyen { get => _strCareerNuyen; set => SetField(ref _strCareerNuyen, value); }

    private bool _blnIsCreateMode;
    public bool IsCreateMode { get => _blnIsCreateMode; set => SetField(ref _blnIsCreateMode, value); }

    private string _strCreationBudgetPoolName = string.Empty;
    public string CreationBudgetPoolName { get => _strCreationBudgetPoolName; set => SetField(ref _strCreationBudgetPoolName, value); }

    private string _strCreationBudgetStarting = "0";
    public string CreationBudgetStarting { get => _strCreationBudgetStarting; set => SetField(ref _strCreationBudgetStarting, value); }

    private string _strCreationBudgetSpent = "0";
    public string CreationBudgetSpent { get => _strCreationBudgetSpent; set => SetField(ref _strCreationBudgetSpent, value); }

    private string _strCreationBudgetRemaining = "0";
    public string CreationBudgetRemaining { get => _strCreationBudgetRemaining; set => SetField(ref _strCreationBudgetRemaining, value); }

    private bool _blnCreationBudgetOverdrawn;
    public bool CreationBudgetOverdrawn
    {
        get => _blnCreationBudgetOverdrawn;
        set
        {
            if (SetField(ref _blnCreationBudgetOverdrawn, value))
                OnPropertyChanged(nameof(CreationBudgetRemainingBrush));
        }
    }
    public string CreationBudgetRemainingBrush => CreationBudgetOverdrawn ? "#B00020" : "Black";

    public ObservableCollection<CreationBudgetCategoryViewModel> CreationBudgetCategories { get; } = new();

    public ObservableCollection<CommlinkItemViewModel> Commlinks { get; } = new();

    private CommlinkItemViewModel? _objSelectedCommlink;
    public CommlinkItemViewModel? SelectedCommlink
    {
        get => _objSelectedCommlink;
        set
        {
            if (!SetField(ref _objSelectedCommlink, value) || value == null || _objCharacter == null || _blnUpdatingCommlinks)
                return;

            _objCharacter.SetActiveCommlink(value.Guid);
            ReloadCommlinks(_objCharacter);
            SetFrom(MatrixInitiative, _objCharacter.MatrixInitiative);
            SetFrom(MatrixInitiativePasses, _objCharacter.MatrixInitiativePasses);
        }
    }

    public void LoadCharacter(CharacterDocument character)
    {
        if (_objCharacter != null)
            _objCharacter.Changed -= OnCharacterChanged;
        _objCharacter = character;
        _objCharacter.Changed += OnCharacterChanged;

        RemainingNuyen = character.Nuyen + "¥";
        CareerKarma = character.CareerKarma.ToString();
        CareerNuyen = character.CareerNuyen + "¥";

        CharacterConditionData condition = character.Condition;
        Essence = condition.Essence;
        PhysicalDamage = condition.PhysicalDamage;
        StunDamage = condition.StunDamage;
        SetFrom(PhysicalMonitor, condition.PhysicalCm);
        SetFrom(StunMonitor, condition.StunCm);
        ReloadConditionMonitor(PhysicalMonitorBoxes, condition.PhysicalCm.Value, condition.PhysicalDamage);
        ReloadConditionMonitor(StunMonitorBoxes, condition.StunCm.Value, condition.StunDamage);

        CharacterEncumbranceData encumbrance = character.ArmorEncumbrance;
        SetFrom(BallisticArmor, encumbrance.BallisticRating);
        SetFrom(ImpactArmor, encumbrance.ImpactRating);
        SetFrom(BallisticEncumbrance, encumbrance.BallisticPenalty);
        SetFrom(ImpactEncumbrance, encumbrance.ImpactPenalty);

        SetFrom(Initiative, character.Initiative);
        SetFrom(InitiativePasses, character.InitiativePasses);
        SetFrom(AstralInitiative, character.AstralInitiative);
        SetFrom(MatrixInitiative, character.MatrixInitiative);
        SetFrom(MatrixInitiativePasses, character.MatrixInitiativePasses);
        SetFrom(Composure, character.Composure);
        SetFrom(JudgeIntentions, character.JudgeIntentions);
        SetFrom(LiftAndCarry, character.LiftAndCarry);
        SetFrom(Memory, character.Memory);
        SetFrom(DamageResistance, character.DamageResistance);
        WoundModifiers = character.WoundModifiers.ToString();
        WalkMovement = character.WalkMovement;
        SwimMovement = string.IsNullOrEmpty(character.SwimMovement) ? "0" : character.SwimMovement;
        FlyMovement = string.IsNullOrEmpty(character.FlyMovement) ? "0" : character.FlyMovement;
        Edge = character.Edge.Remaining + " von " + character.Edge.Maximum + " verbleibend";
        ReloadCommlinks(character);
        IsCreateMode = !character.Created;
        ReloadCreationBudget(character);
    }

    public CharacterDocument? Character => _objCharacter;

    public void FinalizeCreation() { if (_objCharacter?.FinalizeCreation() == true) LoadCharacter(_objCharacter); }

    public void FinalizeCreation(int intLifestyleNuyenDiceResult)
    {
        if (_objCharacter?.FinalizeCreationWithLifestyleNuyenRoll(intLifestyleNuyenDiceResult) == true)
            LoadCharacter(_objCharacter);
    }

    public void SpendEdge() { if (_objCharacter?.SpendEdge() == true) LoadCharacter(_objCharacter); }
    public void RegainEdge() { if (_objCharacter?.RegainEdge() == true) LoadCharacter(_objCharacter); }
    public void AddPhysicalDamage() { if (_objCharacter?.AdjustPhysicalDamage(1) == true) LoadCharacter(_objCharacter); }
    public void HealPhysicalDamage() { if (_objCharacter?.AdjustPhysicalDamage(-1) == true) LoadCharacter(_objCharacter); }
    public void AddStunDamage() { if (_objCharacter?.AdjustStunDamage(1) == true) LoadCharacter(_objCharacter); }
    public void HealStunDamage() { if (_objCharacter?.AdjustStunDamage(-1) == true) LoadCharacter(_objCharacter); }

    private void OnCharacterChanged()
    {
        if (_objCharacter != null)
            LoadCharacter(_objCharacter);
    }

    private void ReloadCommlinks(CharacterDocument character)
    {
        _blnUpdatingCommlinks = true;
        Commlinks.Clear();
        foreach (CharacterCommlinkData objCommlink in character.Commlinks)
            Commlinks.Add(new CommlinkItemViewModel(objCommlink));

        SelectedCommlink = Commlinks.FirstOrDefault(x => x.Active) ?? Commlinks.FirstOrDefault();
        _blnUpdatingCommlinks = false;
    }

    private void ReloadCreationBudget(CharacterDocument character)
    {
        CreationBudgetCategories.Clear();
        if (character.Created)
        {
            CreationBudgetPoolName = string.Empty;
            CreationBudgetStarting = "0";
            CreationBudgetSpent = "0";
            CreationBudgetRemaining = "0";
            CreationBudgetOverdrawn = false;
            return;
        }

        CharacterCreationBudgetData budget = character.CreationBudget;
        CreationBudgetPoolName = string.Equals(budget.BuildMethod, "BP", StringComparison.OrdinalIgnoreCase)
            ? App.LanguageCatalog.GetString("String_BP")
            : App.LanguageCatalog.GetString("String_Karma");
        CreationBudgetStarting = budget.Starting.ToString();
        CreationBudgetSpent = budget.Spent.ToString();
        CreationBudgetRemaining = budget.Remaining.ToString();
        CreationBudgetOverdrawn = budget.Remaining < 0;
        foreach (CharacterCreationBudgetCategoryData category in budget.Categories)
            CreationBudgetCategories.Add(new CreationBudgetCategoryViewModel(category));
    }

    private static void ReloadConditionMonitor(ObservableCollection<ConditionMonitorBoxViewModel> boxes,
        int intMaximum, string strDamage)
    {
        int intFilled = int.TryParse(strDamage, out var intValue) ? Math.Clamp(intValue, 0, Math.Max(0, intMaximum)) : 0;
        boxes.Clear();
        for (int intIndex = 1; intIndex <= Math.Max(0, intMaximum); intIndex++)
            boxes.Add(new ConditionMonitorBoxViewModel(intIndex <= intFilled, intIndex));
    }

    private static void SetFrom(DerivedValueViewModel target, CharacterDerivedValueData data)
    {
        target.Value = data.Value.ToString();
        target.Tooltip = data.Tooltip;
    }

    private static void SetFrom(DerivedValueViewModel target, CharacterInitiativeData data)
    {
        target.Value = data.Display;
        target.Tooltip = data.Tooltip;
    }
}

public sealed class CreationBudgetCategoryViewModel
{
    internal CreationBudgetCategoryViewModel(CharacterCreationBudgetCategoryData objData)
    {
        Name = objData.Name;
        Cost = objData.Cost;
    }

    public string Name { get; }
    public int Cost { get; }
    public string DisplayCost => (Cost >= 0 ? "+" : string.Empty) + Cost;
}

public sealed class CommlinkItemViewModel
{
    internal CommlinkItemViewModel(CharacterCommlinkData objData)
    {
        Guid = objData.Guid;
        Name = objData.Name;
        Response = objData.Response;
        Equipped = objData.Equipped;
        Active = objData.Active;
    }

    public string Guid { get; }
    public string Name { get; }
    public int Response { get; }
    public bool Equipped { get; }
    public bool Active { get; }
    public string DisplayName => Equipped ? Name + " (R " + Response + ")" : Name + " (nicht ausgerüstet)";
}

public sealed class ConditionMonitorBoxViewModel
{
    internal ConditionMonitorBoxViewModel(bool blnFilled, int intPosition)
    {
        IsFilled = blnFilled;
        Tooltip = App.LanguageCatalog.GetString("UI_ConditionMonitorBoxTooltip") + intPosition + (blnFilled ? App.LanguageCatalog.GetString("UI_ConditionMonitorBoxDamaged") : App.LanguageCatalog.GetString("UI_ConditionMonitorBoxFree"));
    }

    public bool IsFilled { get; }
    public string Tooltip { get; }
    public string Background => IsFilled ? "#B64C4C" : "White";
}
