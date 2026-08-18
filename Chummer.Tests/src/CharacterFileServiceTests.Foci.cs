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
    public void CreateStackedFocus_ReplacesUnbondedFociAndEnforcesForceCap()
    {
        Guid first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        CharacterDocument character = LoadXml("<character><gears>"
            + "<gear><guid>" + first + "</guid><name>Power Focus</name><category>Foci</category><rating>2</rating><cost>50000</cost></gear>"
            + "<gear><guid>" + second + "</guid><name>Weapon Focus</name><category>Foci</category><rating>3</rating><cost>15000</cost></gear>"
            + "</gears></character>");

        Assert.True(character.CreateStackedFocus(new[] { first, second }));
        Assert.Single(character.Gear);
        CharacterStackedFocusData stack = Assert.Single(character.StackedFoci);
        Assert.Equal(5, stack.TotalForce);
        Assert.False(stack.Bonded);
        Assert.True(stack.CompositeGearExists);
        Assert.Equal("Stacked Focus", character.Gear[0].Category);
        Assert.False(character.CreateStackedFocus(new[] { first, second }));
        Assert.True(character.UnstackFocus(Guid.Parse(stack.Guid)));
        Assert.Empty(character.StackedFoci);
        Assert.Equal(2, character.Gear.Count);
        Assert.Contains(character.Gear, gear => gear.Name == "Power Focus");

        CharacterDocument capped = LoadXml("<character><gears><gear><guid>" + first
            + "</guid><category>Foci</category><rating>4</rating></gear><gear><guid>" + second
            + "</guid><category>Foci</category><rating>3</rating></gear></gears></character>");
        Assert.False(capped.CreateStackedFocus(new[] { first, second }));
        capped.SetCharacterOptionsForTesting(new CharacterOptions { AllowHigherStackedFoci = true });
        Assert.True(capped.CreateStackedFocus(new[] { first, second }));
    }

    [Fact]
    public void BindStackedFocus_ChargesCombinedCostAndAppliesEquippedBonuses()
    {
        Guid compositeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid stackId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        CharacterDocument character = LoadXml("<character><karma>30</karma><attributes>" + AttributeXml("MAG", "3")
            + "</attributes><gears><gear><guid>" + compositeId + "</guid><name>Stacked Focus</name><category>Stacked Focus</category>"
            + "<equipped>True</equipped></gear></gears><stackedfoci><stackedfocus><guid>" + stackId + "</guid>"
            + "<gearid>" + compositeId + "</gearid><bonded>False</bonded><gears>"
            + "<gear><name>Power Focus</name><category>Foci</category><rating>2</rating></gear>"
            + "<gear><name>Weapon Focus</name><category>Foci</category><rating>3</rating></gear>"
            + "</gears></stackedfocus></stackedfoci></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { KarmaPowerFocus = 8, KarmaWeaponFocus = 3 });

        Assert.True(character.BindStackedFocus(stackId));
        Assert.True(Assert.Single(character.StackedFoci).Bonded);
        Assert.Equal("5", character.Karma);
        Assert.Equal("StackedFocus", character.Document.SelectSingleNode("/character/improvements/improvement/improvementsource")!.InnerText);
        Assert.Equal(stackId.ToString(), character.Document.SelectSingleNode("/character/improvements/improvement/sourcename")!.InnerText);
        Assert.Equal("-25", Assert.Single(character.KarmaExpenses).Amount);
        Assert.Equal(stackId.ToString(), character.Document.SelectSingleNode("/character/expenses/expense/undo/objectid")!.InnerText);
        Assert.False(character.UnstackFocus(stackId));

        Assert.True(character.UnbindStackedFocus(stackId));
        Assert.False(Assert.Single(character.StackedFoci).Bonded);
        Assert.Null(character.Document.SelectSingleNode("/character/improvements/improvement"));
        Assert.True(character.UnstackFocus(stackId));
    }

    [Fact]
    public void CanBondFocus_EnforcesMagCountAndTotalForce()
    {
        CharacterDocument character = LoadXml("<character><attributes>" + AttributeXml("MAG", "2") + "</attributes><gears>"
            + "<gear><guid>00000000-0000-0000-0000-000000000001</guid><category>Foci</category><rating>5</rating></gear>"
            + "<gear><guid>00000000-0000-0000-0000-000000000002</guid><category>Foci</category><rating>6</rating></gear>"
            + "</gears><foci><focus><guid>00000000-0000-0000-0000-000000000010</guid><gearid>00000000-0000-0000-0000-000000000001</gearid><rating>5</rating></focus></foci></character>");

        Assert.False(character.CanBondFocus(Guid.Parse("00000000-0000-0000-0000-000000000001")));
        Assert.False(character.CanBondFocus(Guid.Parse("00000000-0000-0000-0000-000000000002")));
    }

    [Fact]
    public void BindAndUnbindFocus_WritesLegacyRecordsAndKarmaHistory()
    {
        Guid gearId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        CharacterDocument character = LoadXml("<character><karma>20</karma><attributes>" + AttributeXml("MAG", "2")
            + "</attributes><gears><gear><guid>" + gearId + "</guid><name>Power Focus</name><category>Foci</category>"
            + "<rating>2</rating><equipped>False</equipped></gear></gears></character>");
        character.SetCharacterOptionsForTesting(new CharacterOptions { KarmaPowerFocus = 8 });

        Assert.Equal(16, character.GetFocusBindingKarmaCost(gearId));
        Assert.True(character.BindFocus(gearId));
        Assert.Equal("4", character.Karma);
        CharacterFocusData focus = Assert.Single(character.Foci);
        Assert.Equal(gearId.ToString(), focus.GearId);
        Assert.Equal("Power Focus (Force 2)", focus.Name);
        Assert.Equal("True", character.Document.SelectSingleNode("/character/gears/gear/bonded")!.InnerText);
        Assert.Equal("-16", Assert.Single(character.KarmaExpenses).Amount);
        Assert.Equal("BindFocus", character.Document.SelectSingleNode("/character/expenses/expense/undo/karmatype")!.InnerText);
        Assert.Equal(gearId.ToString(), character.Document.SelectSingleNode("/character/expenses/expense/undo/objectid")!.InnerText);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "focus.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "focus.chum");
        Assert.Equal("Power Focus (Force 2)", Assert.Single(reloaded.Foci).Name);
        Assert.Equal("4", reloaded.Karma);

        Assert.True(character.SetGearEquipped(0, true));
        Assert.Equal("Gear", character.Document.SelectSingleNode("/character/improvements/improvement/improvementsource")!.InnerText);
        Assert.Equal(gearId.ToString(), character.Document.SelectSingleNode("/character/improvements/improvement/sourcename")!.InnerText);

        Assert.True(character.UnbindFocus(Guid.Parse(focus.Guid)));
        Assert.Empty(character.Foci);
        Assert.Equal("False", character.Document.SelectSingleNode("/character/gears/gear/bonded")!.InnerText);
        Assert.Null(character.Document.SelectSingleNode("/character/improvements/improvement"));
        Assert.Equal("4", character.Karma);
    }

}
