using System.IO;
using System.Linq;
using System.Text;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class CyberwareDialogViewModelTests
{
    private static CharacterDocument LoadCharacter(string strXml = "<character><name>Runner</name></character>")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void LoadOptions_Cyberware_DefaultsToStandardGradeAndListsAlphaBetaDelta()
    {
        var viewModel = new CyberwareDialogViewModel();
        viewModel.LoadOptions(blnBioware: false, LoadCharacter());

        Assert.Equal("Standard", viewModel.SelectedGrade?.Name);
        Assert.Contains(viewModel.Grades, g => g.Name == "Alphaware");
        Assert.Contains(viewModel.Grades, g => g.Name == "Betaware");
        Assert.Contains(viewModel.Grades, g => g.Name == "Deltaware");
    }

    [Fact]
    public void SelectingBetawareGrade_MultipliesEssenceAndCost_ForARatedItem()
    {
        var viewModel = new CyberwareDialogViewModel();
        viewModel.LoadOptions(blnBioware: true, LoadCharacter());
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
        viewModel.LoadOptions(blnBioware: true, LoadCharacter());
        viewModel.SelectedCyberware = viewModel.CyberwareOptions.Single(o => o.Name == "Cerebral Booster");
        viewModel.SelectedGrade = viewModel.Grades.Single(g => g.Name == "Alphaware");

        viewModel.SelectedCyberware.RatingValue = 1;
        string strAtRatingOne = viewModel.FinalEssence;

        viewModel.SelectedCyberware.RatingValue = 3;
        string strAtRatingThree = viewModel.FinalEssence;

        Assert.NotEqual(strAtRatingOne, strAtRatingThree);
        Assert.Equal("0.48", strAtRatingThree);
    }

    [Fact]
    public void EssenceDiscount_OnlyAppliesWhenTheHouseRuleIsOn()
    {
        CharacterDocument offCharacter = LoadCharacter();
        offCharacter.SetCharacterOptionsForTesting(new CharacterOptions { AllowCyberwareEssDiscounts = false });
        var offViewModel = new CyberwareDialogViewModel();
        offViewModel.LoadOptions(blnBioware: true, offCharacter);
        offViewModel.SelectedCyberware = offViewModel.CyberwareOptions.Single(o => o.Name == "Cerebral Booster");
        offViewModel.SelectedCyberware.RatingValue = 2;

        Assert.False(offViewModel.AllowEssenceDiscount);
        offViewModel.EssenceDiscountPercent = 50;
        Assert.Equal(offViewModel.SelectedCyberware.Essence, offViewModel.FinalEssence);

        CharacterDocument onCharacter = LoadCharacter();
        onCharacter.SetCharacterOptionsForTesting(new CharacterOptions { AllowCyberwareEssDiscounts = true });
        var onViewModel = new CyberwareDialogViewModel();
        onViewModel.LoadOptions(blnBioware: true, onCharacter);
        onViewModel.SelectedCyberware = onViewModel.CyberwareOptions.Single(o => o.Name == "Cerebral Booster");
        onViewModel.SelectedCyberware.RatingValue = 2;

        Assert.True(onViewModel.AllowEssenceDiscount);
        onViewModel.EssenceDiscountPercent = 50;
        Assert.Equal("0.2", onViewModel.FinalEssence); // 0.4 base at rating 2, halved
    }

    [Fact]
    public void CustomTransgenic_OnlyAppearsForEnabledBiowareAndForcesStandardGrade()
    {
        CharacterDocument character = LoadCharacter();
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowCustomTransgenics = true });
        var bioware = new CyberwareDialogViewModel();
        bioware.LoadOptions(blnBioware: true, character);
        bioware.SelectedGrade = bioware.Grades.Single(g => g.Name == "Betaware");
        bioware.IsTransgenic = true;

        Assert.True(bioware.AllowCustomTransgenics);
        Assert.False(bioware.CanSelectGrade);
        Assert.Equal("Standard", bioware.SelectedGrade?.Name);

        var cyberware = new CyberwareDialogViewModel();
        cyberware.LoadOptions(blnBioware: false, character);
        Assert.False(cyberware.AllowCustomTransgenics);
    }
}
