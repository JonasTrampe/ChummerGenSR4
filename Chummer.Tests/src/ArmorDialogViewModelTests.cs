using System.Linq;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class ArmorDialogViewModelTests
{
    [Fact]
    public void LoadOptions_ClothingHasAVariableCostRange_DefaultingToTheMinimum()
    {
        var viewModel = new ArmorDialogViewModel();
        viewModel.LoadOptions();

        ArmorOptionViewModel clothing = viewModel.ArmorOptions.Single(o => o.Name == "Clothing");
        Assert.True(clothing.IsCostVariable);
        Assert.Equal(20, clothing.MinCost);
        Assert.Equal(100000, clothing.MaxCost);
        Assert.Equal("20", clothing.Cost);
    }

    [Fact]
    public void LoadOptions_FixedCostItemIsNotVariable()
    {
        var viewModel = new ArmorDialogViewModel();
        viewModel.LoadOptions();

        ArmorOptionViewModel jacket = viewModel.ArmorOptions.Single(o => o.Name == "Leather Jacket");
        Assert.False(jacket.IsCostVariable);
        Assert.Equal("200", jacket.Cost);
    }
}
