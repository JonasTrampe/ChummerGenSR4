using System;

namespace Chummer.NewUI.ViewModels;

/// <summary>Backs the "build a custom Nexus" dialog (frmSelectNexus.cs's equivalent) - mirrors
/// its NumericUpDown bounds (Processor 10-50, System/Response/Firewall/Signal 1-10, Persona
/// 3-50) and its CalculateNexus cost preview. The actual Gear item is built by <see
/// cref="Chummer.Core.CharacterFileService.AddNexus"/>, which reproduces the same formulas
/// (including the Response 7-10 cost bug) so the preview here always matches what gets added.</summary>
public sealed class NexusDialogViewModel : ViewModelBase
{
    private decimal _decProcessor = 10;
    private decimal _decSystem = 1;
    private decimal _decResponse = 1;
    private decimal _decFirewall = 1;
    private decimal _decSignal = 3;
    private decimal _decPersona = 3;
    private bool _blnFree;

    public decimal Processor { get => _decProcessor; set { if (SetField(ref _decProcessor, value)) RaiseCostChanged(); } }
    public decimal System { get => _decSystem; set { if (SetField(ref _decSystem, value)) RaiseCostChanged(); } }
    public decimal Response { get => _decResponse; set { if (SetField(ref _decResponse, value)) RaiseCostChanged(); } }
    public decimal Firewall { get => _decFirewall; set { if (SetField(ref _decFirewall, value)) RaiseCostChanged(); } }
    public decimal Signal { get => _decSignal; set { if (SetField(ref _decSignal, value)) RaiseCostChanged(); } }
    public decimal Persona { get => _decPersona; set { if (SetField(ref _decPersona, value)) RaiseCostChanged(); } }
    public bool Free { get => _blnFree; set { if (SetField(ref _blnFree, value)) RaiseCostChanged(); } }

    private void RaiseCostChanged() => OnPropertyChanged(nameof(CostPreview));

    public string CostPreview => Free ? "0¥" : $"{CalculateCost():###,###,##0}¥";

    private int CalculateCost()
    {
        int intProcessor = (int)Processor, intSystem = (int)System, intResponse = (int)Response,
            intFirewall = (int)Firewall, intSignal = (int)Signal, intPersona = (int)Persona;

        int intResponseCost = intResponse <= 3 ? intResponse * intProcessor * 50
            : intResponse <= 6 ? intResponse * intProcessor * 100
            : 0; // legacy bug, faithfully reproduced - see AddNexus.

        int intSystemCost = intSystem <= 3 ? intSystem * intPersona * 25
            : intSystem <= 6 ? intSystem * intPersona * 50
            : intSystem * intPersona * 300;

        int intFirewallCost = intFirewall <= 3 ? intFirewall * intProcessor * 25
            : intFirewall <= 6 ? intFirewall * intProcessor * 50
            : intFirewall * intProcessor * 250;

        int intSignalCost = intSignal switch
        {
            2 => 50, 3 => 150, 4 => 500, 5 => 1000, 6 => 3000,
            7 => 6500, 8 => 8750, 9 => 12250, 10 => 17250, _ => 10,
        };

        return intResponseCost + intSystemCost + intFirewallCost + intSignalCost;
    }
}
