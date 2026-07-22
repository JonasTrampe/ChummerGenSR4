using System.Collections.ObjectModel;
using System;
using Chummer.NewUI.Dialogs;

namespace Chummer.NewUI.ViewModels;

public sealed class KarmaGpDialogViewModel : ViewModelBase
{
    private bool _blnIsUpdatingDefaults;
    private string _strDefaultBuildMethod = "BP";
    private int _intDefaultBuildPoints = 400;

    public ObservableCollection<string> BuildMethods { get; } = new()
    {
        "BP",
        "Karma"
    };

    private string _buildMethod = "BP";
    public string BuildMethod
    {
        get => _buildMethod;
        set
        {
            if (!SetField(ref _buildMethod, value))
                return;

            if (!_blnIsUpdatingDefaults)
            {
                if (string.Equals(value, _strDefaultBuildMethod, StringComparison.OrdinalIgnoreCase))
                    BuildPoints = _intDefaultBuildPoints;
                else if (string.Equals(value, "Karma", StringComparison.OrdinalIgnoreCase))
                    BuildPoints = 750;
                else
                    BuildPoints = 400;
            }

            OnPropertyChanged(nameof(Description));
        }
    }

    private int _buildPoints = 400;
    public int BuildPoints
    {
        get => _buildPoints;
        set => SetField(ref _buildPoints, value);
    }

    private int _maxAvailability = 12;
    public int MaxAvailability
    {
        get => _maxAvailability;
        set => SetField(ref _maxAvailability, value);
    }

    private bool _ignoreCreationRules;
    public bool IgnoreCreationRules
    {
        get => _ignoreCreationRules;
        set => SetField(ref _ignoreCreationRules, value);
    }

    public string Description => BuildMethod == "Karma"
        ? "Geben Sie die Menge an Karma ein mit der Sie Ihren Charakter erschaffen dürfen (Gewöhnlich 750)."
        : "Geben Sie die Menge an Generierungspunkten ein mit der Sie Ihren Charakter erschaffen dürfen (Gewöhnlich 400).";

    public void ApplyDefaults(SettingsProfileSelection objSelection)
    {
        _blnIsUpdatingDefaults = true;
        _strDefaultBuildMethod = string.IsNullOrWhiteSpace(objSelection.BuildMethod) ? "BP" : objSelection.BuildMethod;
        _intDefaultBuildPoints = objSelection.BuildPoints > 0 ? objSelection.BuildPoints : string.Equals(_strDefaultBuildMethod, "Karma", StringComparison.OrdinalIgnoreCase) ? 750 : 400;

        BuildMethod = _strDefaultBuildMethod;
        BuildPoints = _intDefaultBuildPoints;
        MaxAvailability = objSelection.MaxAvailability > 0 ? objSelection.MaxAvailability : 12;
        _blnIsUpdatingDefaults = false;
        OnPropertyChanged(nameof(Description));
    }
}
