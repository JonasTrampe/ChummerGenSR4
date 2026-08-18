namespace Chummer.NewUI.ViewModels;

/// <summary>One entry from cyberware.xml/bioware.xml's shared &lt;grades&gt; list - Standard,
/// Alphaware, Betaware, Deltaware, and their Second-Hand/Adapsin variants. Multiplies a piece of
/// Cyber-/Bioware's own Essence and cost, and adds a flat modifier to its availability.</summary>
public sealed class GradeOptionViewModel
{
    public GradeOptionViewModel(string name, double essMultiplier, double costMultiplier, int availModifier)
    {
        Name = name;
        EssMultiplier = essMultiplier;
        CostMultiplier = costMultiplier;
        AvailModifier = availModifier;
    }

    public string Name { get; }
    public double EssMultiplier { get; }
    public double CostMultiplier { get; }
    public int AvailModifier { get; }

    public override string ToString() => Name;
}
