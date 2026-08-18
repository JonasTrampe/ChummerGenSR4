using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

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
