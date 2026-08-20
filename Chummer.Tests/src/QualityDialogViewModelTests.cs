using System.IO;
using System.Text;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public class QualityDialogViewModelTests
{
    private static CharacterDocument LoadXml(string strXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(strXml));
        return new CharacterFileService().Load(stream, "test.chum");
    }

    [Fact]
    public void LoadOptions_DefaultsToPositiveOnlyWithNoAllOption()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        var viewModel = new QualityDialogViewModel();

        viewModel.LoadOptions(character);

        Assert.Equal(2, viewModel.CategoryFilterOptions.Count);
        Assert.All(viewModel.QualityOptions, option => Assert.Equal("Positive", option.Category));
        Assert.NotEmpty(viewModel.QualityOptions);
    }

    [Fact]
    public void CategoryFilter_SwitchingToNegativeShowsOnlyNegativeQualities()
    {
        CharacterDocument character = LoadXml("<character><name>Runner</name></character>");
        var viewModel = new QualityDialogViewModel();
        viewModel.LoadOptions(character);

        viewModel.CategoryFilter = viewModel.CategoryFilterOptions[1];

        Assert.NotEmpty(viewModel.QualityOptions);
        Assert.All(viewModel.QualityOptions, option => Assert.Equal("Negative", option.Category));
    }
}
