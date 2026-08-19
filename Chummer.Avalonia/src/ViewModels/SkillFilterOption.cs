using System;
using Chummer.Core;

namespace Chummer.NewUI.ViewModels;

/// <summary>One entry in the Aktionsfertigkeiten filter dropdown - a label plus the predicate it
/// applies to the unfiltered skill list, matching the legacy frmCareer.cs/frmCreate.cs filter combo.</summary>
public sealed class SkillFilterOption
{
    public string Label { get; }
    public Func<CharacterSkillData, bool> Predicate { get; }

    public SkillFilterOption(string strLabel, Func<CharacterSkillData, bool> predicate)
    {
        Label = strLabel;
        Predicate = predicate;
    }

    public override string ToString() => Label;
}
