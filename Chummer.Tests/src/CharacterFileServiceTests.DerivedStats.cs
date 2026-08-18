using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Chummer.Core;
using Chummer.NewUI.ViewModels;
using Xunit;

namespace Chummer.Tests;

public partial class CharacterFileServiceTests
{
    [Fact]
    public void WoundModifiers_ApplyBothConditionMonitorTracks()
    {
        CharacterDocument character = LoadXml("<character><physicalcmfilled>3</physicalcmfilled><stuncmfilled>4</stuncmfilled></character>");

        Assert.Equal(-3, character.WoundModifiers);
    }

    [Fact]
    public void SetMysticAdeptMagicianMagSplit_SubtractsEssencePenaltyFromTheAdeptShare()
    {
        // Ported from frmCareer.cs's nudMysticAdeptMAGMagician_ValueChanged: MAGAdept =
        // MAG.Value - MAGMagician - EssencePenalty. 6 ESS max - 2.5 installed -> penalty of 3.
        var character = LoadXml("<character><adept>True</adept><magician>True</magician><attributes>"
            + AttributeXml("ESS", "6") + AttributeXml("MAG", "6") + "</attributes><cyberwares>"
            + "<cyberware><name>Wired Reflexes</name><ess>2.5</ess><improvementsource>Cyberware</improvementsource></cyberware>"
            + "</cyberwares></character>");
        Assert.Equal(3, character.EssencePenalty);

        Assert.True(character.SetMysticAdeptMagicianMagSplit(2));

        Assert.Equal(2, character.MysticAdeptMagicianMagSplit);
        Assert.Equal(1, character.MysticAdeptAdeptMagSplit); // 6 - 2 - EssencePenalty of 3.
    }

    [Fact]
    public void RaiseNuyenCreate_StopsAtTheSavedMaxByDefault()
    {
        CharacterDocument character = LoadXml(
            "<character><buildmethod>Bp</buildmethod><bp>10</bp><nuyenbp>2</nuyenbp><nuyenmaxbp>2</nuyenmaxbp></character>");
        Assert.False(character.RaiseNuyenCreate());
        Assert.Equal(2, character.NuyenPoints);
    }

}
