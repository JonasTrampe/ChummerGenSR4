using System.Collections.ObjectModel;
using System.Linq;

namespace Chummer.NewUI.ViewModels;

/// <summary>One entry in a Group-modifier dropdown - a label plus the point value it contributes
/// to the contact's Group Rating.</summary>
public sealed class GroupModifierOption
{
    public GroupModifierOption(string strLabel, int intValue)
    {
        Label = strLabel;
        Value = intValue;
    }

    public string Label { get; }
    public int Value { get; }
    public override string ToString() => Label;
}

/// <summary>Backs the "Zusätzliche Connectioneinstellungen" dialog - ported from
/// frmSelectContactConnection.cs's fixed point values for Membership/Area of Influence/Magical
/// Resources/Matrix Resources.</summary>
public sealed class ContactGroupDialogViewModel : ViewModelBase
{
    public ObservableCollection<GroupModifierOption> MembershipOptions { get; } = new()
    {
        new GroupModifierOption("+0: -", 0),
        new GroupModifierOption("+1: 2-19 Mitglieder", 1),
        new GroupModifierOption("+2: 20-99 Mitglieder", 2),
        new GroupModifierOption("+4: 100-1000 Mitglieder", 4),
        new GroupModifierOption("+6: 1000+ Mitglieder", 6),
    };

    public ObservableCollection<GroupModifierOption> AreaOfInfluenceOptions { get; } = new()
    {
        new GroupModifierOption("+0: -", 0),
        new GroupModifierOption("+1: Bezirk", 1),
        new GroupModifierOption("+2: Stadtweit", 2),
        new GroupModifierOption("+4: National", 4),
        new GroupModifierOption("+6: Global", 6),
    };

    public ObservableCollection<GroupModifierOption> MagicalResourcesOptions { get; } = new()
    {
        new GroupModifierOption("+0: -", 0),
        new GroupModifierOption("+1: Magische Minderheit", 1),
        new GroupModifierOption("+4: Größtenteils magisch", 4),
        new GroupModifierOption("+6: Umfangreiche magische Ressourcen", 6),
    };

    public ObservableCollection<GroupModifierOption> MatrixResourcesOptions { get; } = new()
    {
        new GroupModifierOption("+0: -", 0),
        new GroupModifierOption("+1: Matrix-Aktiv", 1),
        new GroupModifierOption("+2: Breite Matrixpräsenz", 2),
        new GroupModifierOption("+4: Durchdringende Matrixintegration", 4),
    };

    private string _strGroupName = string.Empty;
    public string GroupName
    {
        get => _strGroupName;
        set => SetField(ref _strGroupName, value);
    }

    private GroupModifierOption? _selectedMembership;
    public GroupModifierOption? SelectedMembership
    {
        get => _selectedMembership;
        set
        {
            if (!SetField(ref _selectedMembership, value))
                return;
            OnPropertyChanged(nameof(TotalConnectionModifier));
        }
    }

    private GroupModifierOption? _selectedAreaOfInfluence;
    public GroupModifierOption? SelectedAreaOfInfluence
    {
        get => _selectedAreaOfInfluence;
        set
        {
            if (!SetField(ref _selectedAreaOfInfluence, value))
                return;
            OnPropertyChanged(nameof(TotalConnectionModifier));
        }
    }

    private GroupModifierOption? _selectedMagicalResources;
    public GroupModifierOption? SelectedMagicalResources
    {
        get => _selectedMagicalResources;
        set
        {
            if (!SetField(ref _selectedMagicalResources, value))
                return;
            OnPropertyChanged(nameof(TotalConnectionModifier));
        }
    }

    private GroupModifierOption? _selectedMatrixResources;
    public GroupModifierOption? SelectedMatrixResources
    {
        get => _selectedMatrixResources;
        set
        {
            if (!SetField(ref _selectedMatrixResources, value))
                return;
            OnPropertyChanged(nameof(TotalConnectionModifier));
        }
    }

    public int TotalConnectionModifier =>
        (SelectedMembership?.Value ?? 0) + (SelectedAreaOfInfluence?.Value ?? 0)
        + (SelectedMagicalResources?.Value ?? 0) + (SelectedMatrixResources?.Value ?? 0);

    public void LoadFrom(ContactRowViewModel contact)
    {
        GroupName = contact.GroupName;
        SelectedMembership = MembershipOptions.FirstOrDefault(o => o.Value == contact.Membership) ?? MembershipOptions[0];
        SelectedAreaOfInfluence = AreaOfInfluenceOptions.FirstOrDefault(o => o.Value == contact.AreaOfInfluence) ?? AreaOfInfluenceOptions[0];
        SelectedMagicalResources = MagicalResourcesOptions.FirstOrDefault(o => o.Value == contact.MagicalResources) ?? MagicalResourcesOptions[0];
        SelectedMatrixResources = MatrixResourcesOptions.FirstOrDefault(o => o.Value == contact.MatrixResources) ?? MatrixResourcesOptions[0];
    }
}
