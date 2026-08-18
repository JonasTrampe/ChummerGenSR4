using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

public sealed class ComplexFormRowViewModel
{
    public string Guid { get; }
    public string Label { get; }
    public string Value { get; }
    public string Category { get; }
    public string Notes { get; }

    public ComplexFormRowViewModel(CharacterComplexFormData form, int intKarmaCost)
    {
        Guid = form.Guid;
        Label = form.DisplayName;
        Value = form.Rating + " · " + intKarmaCost + " Karma";
        Category = form.Category;
        Notes = form.Notes;
    }
}
