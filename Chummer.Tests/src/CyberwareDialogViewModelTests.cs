using System.Linq;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class CyberwareDialogViewModelTests
{
    [Fact]
    public void LoadOptions_Cyberware_DefaultsToStandardGradeAndListsAlphaBetaDelta()
    {
        var viewModel = new CyberwareDialogViewModel();
        viewModel.LoadOptions(blnBioware: false);

        Assert.Equal("Standard", viewModel.SelectedGrade?.Name);
        Assert.Contains(viewModel.Grades, g => g.Name == "Alphaware");
        Assert.Contains(viewModel.Grades, g => g.Name == "Betaware");
        Assert.Contains(viewModel.Grades, g => g.Name == "Deltaware");
    }

    [Fact]
    public void SelectingBetawareGrade_MultipliesEssenceAndCost_ForARatedItem()
    {
        var viewModel = new CyberwareDialogViewModel();
        viewModel.LoadOptions(blnBioware: true);
        viewModel.SelectedCyberware = viewModel.CyberwareOptions.Single(o => o.Name == "Cerebral Booster");
        Assert.Equal(3, viewModel.SelectedCyberware.MaxRating);
        viewModel.SelectedCyberware.RatingValue = 2;

        // Standard grade: no multiplier at all - base values pass through unchanged.
        Assert.Equal(viewModel.SelectedCyberware.Essence, viewModel.FinalEssence);
        Assert.Equal(viewModel.SelectedCyberware.Cost, viewModel.FinalCost);

        viewModel.SelectedGrade = viewModel.Grades.Single(g => g.Name == "Betaware");

        // Betaware: Essence x0.7, cost x4 (from cyberware.xml's <grades> multipliers).
        Assert.Equal("0.28", viewModel.FinalEssence);
        Assert.Equal("80000", viewModel.FinalCost);
    }

    [Fact]
    public void ChangingRating_RecomputesFinalValuesForTheCurrentlySelectedGrade()
    {
        var viewModel = new CyberwareDialogViewModel();
        viewModel.LoadOptions(blnBioware: true);
        viewModel.SelectedCyberware = viewModel.CyberwareOptions.Single(o => o.Name == "Cerebral Booster");
        viewModel.SelectedGrade = viewModel.Grades.Single(g => g.Name == "Alphaware");

        viewModel.SelectedCyberware.RatingValue = 1;
        string strAtRatingOne = viewModel.FinalEssence;

        viewModel.SelectedCyberware.RatingValue = 3;
        string strAtRatingThree = viewModel.FinalEssence;

        Assert.NotEqual(strAtRatingOne, strAtRatingThree);
        Assert.Equal("0.48", strAtRatingThree);
    }
}
