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
    public void Vehicles_ReadSavedStatsAndInstalledItemTree()
    {
        CharacterDocument character = LoadXml(
            "<character><vehicles><vehicle>"
            + "<name>Hyundai Shin-Hyung</name><category>Cars</category><handling>3</handling>"
            + "<accel>15</accel><speed>180</speed><pilot>2</pilot><body>10</body><armor>6</armor>"
            + "<sensor>2</sensor><devicerating>3</devicerating><avail>4</avail><cost>16000</cost><addslots>2</addslots>"
            + "<source>SR4</source><page>351</page><physicalcmfilled>1</physicalcmfilled>"
            + "<mods><mod><name>Armor</name><category>Vehicle Mod</category><rating>2</rating><cost>1000</cost></mod></mods>"
            + "<gears><gear><name>Vehicle Toolkit</name><category>Tools</category><qty>1</qty></gear></gears>"
            + "<weapons><weapon><name>LMG</name><category>Machine Guns</category><damage>6P</damage></weapon></weapons>"
            + "</vehicle></vehicles></character>");

        CharacterVehicleData vehicle = Assert.Single(character.Vehicles);
        Assert.Equal("180", vehicle.Speed);
        Assert.Equal("16000", vehicle.Cost);
        Assert.Equal(3, vehicle.Children.Count);
        Assert.Equal("Armor", vehicle.Children[0].Name);
        Assert.Equal("Vehicle Toolkit", vehicle.Children[1].Name);
        Assert.Equal("LMG", vehicle.Children[2].Name);
    }

    [Fact]
    public void AddAndRemoveVehicle_PersistsTheLegacyVehicleShapeAndDeductsCost()
    {
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen></character>");
        character.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "15", "180", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");

        CharacterVehicleData vehicle = Assert.Single(character.Vehicles);
        Assert.Equal("180", vehicle.Speed);
        Assert.Equal("34000", character.Nuyen);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        Assert.Equal("Hyundai Shin-Hyung", Assert.Single(reloaded.Vehicles).Name);
        Assert.True(reloaded.RemoveVehicle("Hyundai Shin-Hyung", "Cars"));
        Assert.Empty(reloaded.Vehicles);
    }

    [Fact]
    public void VehicleTotals_ModsAddFlatBonusesToBodyAndHandling()
    {
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen></character>");
        character.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "3/6", "180", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");
        Guid guiVehicleId = Guid.Parse(character.Vehicles.Single().Guid);

        Assert.True(character.AddVehicleMod(guiVehicleId, "Unstable Structural Agility", "All", "0",
            "4", "12R", "0", "AR", "146"));

        CharacterVehicleData vehicle = character.Vehicles.Single();
        Assert.Equal(10, vehicle.TotalBody); // No <body> bonus on this mod.
        Assert.Equal(3 + 3, vehicle.TotalHandling); // Base 3 + the Mod's flat +3.
    }

    [Fact]
    public void VehicleTotals_ArmorModReplacesRatherThanAddsToTheBaseArmor()
    {
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen></character>");
        character.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "3/6", "180", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");
        Guid guiVehicleId = Guid.Parse(character.Vehicles.Single().Guid);

        Assert.True(character.AddVehicleMod(guiVehicleId, "Armor, Normal", "All", "8",
            "1", "6R", "0", "AR", "132"));

        // Base Armor of 6 is entirely replaced (not added to) by the Mod's own Rating-scaled bonus.
        Assert.Equal(8, character.Vehicles.Single().TotalArmor);
    }

    [Fact]
    public void VehicleTotals_SpeedAndAccelModsApplyAsPercentMultipliers()
    {
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen></character>");
        character.AddVehicle("Hyundai Shin-Hyung", "Cars", "3", "3/6", "100", "2", "10", "6", "2", "3",
            "4", "16000", "SR4", "351");
        Guid guiVehicleId = Guid.Parse(character.Vehicles.Single().Guid);

        Assert.True(character.AddVehicleMod(guiVehicleId, "Engine Customization, Speed", "All", "0",
            "2", "6", "0", "AR", "135"));
        Assert.True(character.AddVehicleMod(guiVehicleId, "Engine Customization, Acceleration", "All", "0",
            "2", "6", "0", "AR", "135"));

        CharacterVehicleData vehicle = character.Vehicles.Single();
        Assert.Equal(120, vehicle.TotalSpeed); // 100 + 100 * 0.2.
        Assert.Equal("4/8", vehicle.TotalAccel); // 3/6 base + 20%, rounded up.
    }

    [Fact]
    public void VehicleTotals_RatingScaledAccelBonusResolvesRatingInTheExpression()
    {
        CharacterDocument character = LoadXml("<character><nuyen>500000</nuyen></character>");
        character.AddVehicle("Bulldog Step Van", "Trucks", "3", "3/6", "100", "2", "12", "6", "2", "3",
            "4", "16000", "SR4", "351");
        Guid guiVehicleId = Guid.Parse(character.Vehicles.Single().Guid);

        Assert.True(character.AddVehicleMod(guiVehicleId, "Turbocharger", "Standard", "4",
            "4", "4", "0", "AR", "146"));

        // "+(Rating * 5)/+(Rating * 10)" at Rating 4 -> +20/+40 on top of the 3/6 base.
        Assert.Equal("23/46", character.Vehicles.Single().TotalAccel);
    }

    [Fact]
    public void VehicleNotes_PersistForTheVehicleAndItsInstalledItem()
    {
        Guid guiVehicleId = Guid.NewGuid();
        Guid guiModId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + guiVehicleId
            + "</guid><name>Americar</name><category>Cars</category><notes>Original owner</notes><mods><mod><guid>"
            + guiModId + "</guid><name>Armor</name><category>Armor</category></mod></mods><gears /><weapons />"
            + "</vehicle></vehicles></character>");

        Assert.True(character.SetVehicleNotes(guiVehicleId, "Loaned to the team"));
        Assert.True(character.SetVehicleItemNotes(guiVehicleId, guiModId, "Scratched after the last run"));
        CharacterVehicleData vehicle = Assert.Single(character.Vehicles);
        Assert.Equal("Loaned to the team", vehicle.Notes);
        Assert.Equal("Scratched after the last run", Assert.Single(vehicle.Children).Notes);

        using var stream = new MemoryStream();
        new CharacterFileService().Save(character, stream, "saved.chum");
        stream.Position = 0;
        CharacterDocument reloaded = new CharacterFileService().Load(stream, "saved.chum");
        CharacterVehicleData reloadedVehicle = Assert.Single(reloaded.Vehicles);
        Assert.Equal("Loaned to the team", reloadedVehicle.Notes);
        Assert.Equal("Scratched after the last run", Assert.Single(reloadedVehicle.Children).Notes);
    }

    [Fact]
    public void CharacterSheetExporter_IncludesVehiclesWithModsGearAndWeaponsInTheExportXml()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category>"
            + "<body>4</body><mods /><gears /><weapons /></vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleGear(vehicleId, "Vehicle Toolkit", "Tools", "0", "1", "250", "4", "SR4", "320");
        character.AddVehicleWeapon(vehicleId, "Mounted Gun", "Machine Pistols", "6P", "0", "SA",
            "0", "20", "500", "8R", "SR4", "100");

        string html = CharacterSheetExporter.RenderSheet(character, "Text-Only.xsl");

        Assert.Contains("Americar", html);
        Assert.Contains("Anti-Theft", html);
        Assert.Contains("Vehicle Toolkit", html);
        Assert.Contains("Mounted Gun", html);
    }

    [Fact]
    public void VehicleDamage_CanBeAdjustedWithoutGoingBelowZero()
    {
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><name>Americar</name><category>Cars</category><physicalcmfilled>1</physicalcmfilled></vehicle></vehicles></character>");
        Assert.True(character.AdjustVehicleDamage("Americar", "Cars", 2));
        Assert.Equal("3", character.Vehicles.Single().PhysicalCmFilled);
        Assert.True(character.AdjustVehicleDamage("Americar", "Cars", -5));
        Assert.Equal("0", character.Vehicles.Single().PhysicalCmFilled);
    }

    [Fact]
    public void VehicleMods_CanBeAddedRemovedAndChargeBodyBasedCost()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods />"
            + "</vehicle></vehicles></character>");

        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R",
            "Body * 200", "AR", "132"));
        CharacterTreeItemData mod = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Anti-Theft", mod.Name);
        Assert.Equal("Standard", mod.Category);
        Assert.Equal(4200d, double.Parse(character.Nuyen, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Guid.TryParse(mod.ItemGuid, out Guid modId));
        Assert.True(character.RemoveVehicleMod(vehicleId, modId));
        Assert.Empty(character.Vehicles.Single().Children);
    }

    [Fact]
    public void ObsoleteVehicleMod_CanBeRetrofittedAtSelectedPercentage()
    {
        Guid vehicleId = Guid.NewGuid();
        Guid modId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><cost>7000</cost><mods><mod>"
            + "<guid>" + modId + "</guid><name>Obsolete</name><category>Special</category><slots>0</slots>"
            + "</mod></mods></vehicle></vehicles></character>");

        Assert.True(character.RetrofitVehicleObsolescence(vehicleId, modId, 50));
        CharacterTreeItemData retrofit = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Retrofit", retrofit.Name);
        Assert.Equal("1500", character.Nuyen);
        CharacterExpenseData expense = Assert.Single(character.NuyenExpenses);
        Assert.Equal("-3500", expense.Amount);
    }

    [Fact]
    public void ObsolescentVehicleMod_RequiresItsHouseRule()
    {
        Guid vehicleId = Guid.NewGuid();
        Guid modId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><cost>1000</cost><mods><mod>"
            + "<guid>" + modId + "</guid><name>Obsolescent</name><category>Special</category><slots>0</slots>"
            + "</mod></mods></vehicle></vehicles></character>");

        Assert.False(character.RetrofitVehicleObsolescence(vehicleId, modId, 10));
        character.SetCharacterOptionsForTesting(new CharacterOptions { AllowObsolescentUpgrade = true });
        Assert.True(character.RetrofitVehicleObsolescence(vehicleId, modId, 10));
        Assert.Equal("Retrofit", Assert.Single(character.Vehicles.Single().Children).Name);
    }

    [Fact]
    public void VehicleSlots_ComputedFromBodyAndSummedAcrossInstalledMods()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>6</body><mods />"
            + "</vehicle></vehicles></character>");

        CharacterVehicleData vehicle = character.Vehicles.Single();
        // Body(6) > 4, so TotalSlots = Body, not the 4-slot floor.
        Assert.Equal(6, vehicle.TotalSlots);
        Assert.Equal(0, vehicle.SlotsUsed);
        Assert.Equal(6, vehicle.SlotsRemaining);

        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132"));
        vehicle = character.Vehicles.Single();
        Assert.Equal(2, vehicle.SlotsUsed);
        Assert.Equal(4, vehicle.SlotsRemaining);
    }

    [Fact]
    public void VehicleTotalCost_SumsBaseCostModsAndOnboardGear()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>50000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>6</body>"
            + "<cost>7000</cost><mods /></vehicle></vehicles></character>");

        Assert.Equal(7000, character.Vehicles.Single().TotalCost);

        // "Body * 200" at Body(6) -> 1200.
        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "Body * 200", "AR", "132"));
        Assert.Equal(8200, character.Vehicles.Single().TotalCost);

        Assert.True(character.AddVehicleGear(vehicleId, "Fake SIN", "Fake Identification", strCost: "500"));
        Assert.Equal(8700, character.Vehicles.Single().TotalCost);
    }

    [Fact]
    public void VehicleSlots_LowBodyVehicleFloorsAtFourSlots()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Dodge Scoot</name><category>Bikes</category><body>2</body><mods />"
            + "</vehicle></vehicles></character>");

        // Body(2) < 4, so TotalSlots floors at 4 (clsEquipment.cs's Vehicle.Slots).
        Assert.Equal(4, character.Vehicles.Single().TotalSlots);
    }

    private static CharacterDocument LoadVehicleWithSensorSuite(string strSavedSensor)
        => LoadXml("<character><vehicles><vehicle><guid>" + Guid.NewGuid() + "</guid><name>Americar</name>"
            + "<category>Cars</category><body>6</body><sensor>" + strSavedSensor + "</sensor><mods />"
            + "<gears><gear><name>Sensor Array</name><category>Sensors</category><signal>3</signal>"
            + "<children>"
            + "<gear><name>Camera</name><category>Sensor Functions</category><rating>4</rating></gear>"
            + "<gear><name>Radio Signal Scanner</name><category>Sensor Functions</category><rating>2</rating></gear>"
            + "</children></gear></gears></vehicle></vehicles></character>");

    [Fact]
    public void CalculatedSensor_AveragesSensorFunctionRatingsOfTheFirstOnboardGearItem()
    {
        CharacterDocument character = LoadVehicleWithSensorSuite("2");
        CharacterVehicleData vehicle = character.Vehicles.Single();

        // (4 + 2) / 2 = 3, rounded up (already whole).
        Assert.Equal(3, vehicle.CalculatedSensor);
    }

    [Fact]
    public void SensorDisplay_OnlyUsesTheCalculatedValueWhenTheHouseRuleIsOn()
    {
        CharacterDocument offCharacter = LoadVehicleWithSensorSuite("2");
        offCharacter.SetCharacterOptionsForTesting(new CharacterOptions { UseCalculatedVehicleSensorRatings = false });
        Assert.Equal("2", offCharacter.Vehicles.Single().SensorDisplay);

        CharacterDocument onCharacter = LoadVehicleWithSensorSuite("2");
        onCharacter.SetCharacterOptionsForTesting(new CharacterOptions { UseCalculatedVehicleSensorRatings = true });
        Assert.Equal("3", onCharacter.Vehicles.Single().SensorDisplay);
    }

    [Fact]
    public void AddVehicleMod_RejectsAModThatWouldExceedRemainingSlots()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods />"
            + "</vehicle></vehicles></character>");

        // TotalSlots = 4 (Body floor). A mod needing 5 slots doesn't fit.
        Assert.False(character.AddVehicleMod(vehicleId, "Oversized Mod", "Standard", "0", "5", "6R",
            "Body * 200", "AR", "132"));
        Assert.Empty(character.Vehicles.Single().Children);

        // But one that exactly fits does, and a second one that would push past the limit is rejected.
        Assert.True(character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "4", "6R",
            "Body * 200", "AR", "132"));
        Assert.False(character.AddVehicleMod(vehicleId, "Another Mod", "Standard", "0", "1", "6R",
            "Body * 200", "AR", "132"));
        Assert.Single(character.Vehicles.Single().Children);
    }

    [Fact]
    public void VehicleMod_IncludedInVehicle_DoesNotCountTowardSlotsUsed()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><body>4</body><mods>"
            + "<mod><guid>" + Guid.NewGuid() + "</guid><name>Standard Chassis</name><category>Standard</category>"
            + "<slots>4</slots><rating>0</rating><included>True</included></mod>"
            + "</mods></vehicle></vehicles></character>");

        CharacterVehicleData vehicle = character.Vehicles.Single();
        Assert.Equal(0, vehicle.SlotsUsed);
        Assert.Equal(4, vehicle.SlotsRemaining);
    }

    [Fact]
    public void VehicleGear_CanBeAddedAndRemovedWithoutEnteringCharacterGearTree()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>5000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><gears />"
            + "</vehicle></vehicles></character>");

        Assert.True(character.AddVehicleGear(vehicleId, "Vehicle Toolkit", "Tools", "0", "2", "250", "4", "SR4", "320"));
        CharacterTreeItemData gear = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal("Vehicle Toolkit", gear.Name);
        Assert.Equal("4500", character.Nuyen);
        Assert.Empty(character.Gear);
        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));
        Assert.True(character.RemoveVehicleGear(vehicleId, gearId));
        Assert.Empty(character.Vehicles.Single().Children);
    }

    [Fact]
    public void VehicleGearPlugin_NestsUnderOnboardGear()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>1000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><gears />"
            + "</vehicle></vehicles></character>");
        Assert.True(character.AddVehicleGear(vehicleId, "Vehicle Toolkit", "Tools", strCost: "250"));
        Guid parentId = Guid.Parse(character.Vehicles.Single().Children.Single().ItemGuid);

        Assert.True(character.AddVehicleGearPlugin(vehicleId, parentId, "Battery", "Electronics", strCost: "50"));
        Assert.Equal("Battery", Assert.Single(character.Vehicles.Single().Children.Single().Children).Name);
        Assert.Equal("700", character.Nuyen);
    }

    [Fact]
    public void VehicleWeapons_CanBeAddedRemovedAndCharged()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        CharacterTreeItemData weapon = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Assert.Equal("6000", character.Nuyen); // 10000 - 1500(mount) - 2500(weapon)
        Assert.True(Guid.TryParse(weapon.ItemGuid, out Guid weaponId));
        Assert.True(character.RemoveVehicleWeapon(vehicleId, weaponId));
        Assert.Single(character.Vehicles.Single().Children); // the mount mod itself is still there
    }

    [Fact]
    public void AddVehicleWeapon_RejectedWithoutAnAvailableMount()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");

        // No Weapon Mount/Mechanical Arm mod installed at all.
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.Empty(character.Vehicles.Single().Children);

        // A regular (non-mount) mod doesn't count.
        character.AddVehicleMod(vehicleId, "Anti-Theft", "Standard", "0", "2", "6R", "200", "AR", "132");
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
    }

    [Fact]
    public void AddVehicleWeapon_LimitsDirectWeaponsToTheInstalledMountCount()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>100000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        // The single mount is already occupied - a second direct weapon is rejected.
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));

        // Installing a second mount frees up room for a second weapon.
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));
    }

    [Fact]
    public void AddVehicleWeapon_TracksWhichSpecificMountEachWeaponOccupies()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>100000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons /><mods />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");

        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Predator IV", "Heavy Pistols", "6P", "-1", "SA",
            "0", "15", "350", "4R", "SR4", "313"));

        var mounts = character.Vehicles.Single().Children.Where(c => c.Name.StartsWith("Weapon Mount")).ToList();
        var weapons = character.Vehicles.Single().Children.Where(c => !c.Name.StartsWith("Weapon Mount")).ToList();
        Assert.Equal(2, mounts.Count);
        Assert.Equal(2, weapons.Count);

        // Each weapon claimed a distinct mount, not just "there were enough mounts overall".
        Guid guiAlphaId = Guid.Parse(weapons.Single(w => w.Name == "Ares Alpha").ItemGuid);
        Assert.True(character.RemoveVehicleWeapon(vehicleId, guiAlphaId));

        // Removing one weapon frees exactly its own mount - a third weapon can now be added even
        // though the Predator IV still occupies the other mount.
        Assert.True(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312"));
        Assert.False(character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312")); // Both mounts occupied again.
    }

    [Fact]
    public void VehicleLocations_CanBeAddedRemovedAndReloaded()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + vehicleId
            + "</guid><name>Americar</name><category>Cars</category></vehicle></vehicles></character>");

        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Contains("Kofferraum", character.Vehicles.Single().Locations);
        Assert.False(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.RemoveVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Empty(character.Vehicles.Single().Locations);
    }

    [Fact]
    public void AssignVehicleGearLocation_MovesGearIntoAndOutOfANamedLocation()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><vehicles><vehicle><guid>" + vehicleId
            + "</guid><name>Americar</name><category>Cars</category></vehicle></vehicles></character>");
        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.AddVehicleGear(vehicleId, "Fake SIN", "Fake Identification"));

        CharacterTreeItemData gear = Assert.Single(character.Vehicles.Single().Children);
        Assert.Equal(string.Empty, gear.Location);
        Assert.True(Guid.TryParse(gear.ItemGuid, out Guid gearId));

        // Assigning to a location that doesn't exist on the vehicle is rejected.
        Assert.False(character.AssignVehicleGearLocation(vehicleId, gearId, "Kein Ort"));

        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, "Kofferraum"));
        Assert.Equal("Kofferraum", character.Vehicles.Single().Children.Single().Location);

        // Removing the location clears the assignment from the gear that referenced it.
        Assert.True(character.RemoveVehicleLocation(vehicleId, "Kofferraum"));
        Assert.Equal(string.Empty, character.Vehicles.Single().Children.Single().Location);

        Assert.True(character.AddVehicleLocation(vehicleId, "Kofferraum"));
        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, "Kofferraum"));
        Assert.True(character.AssignVehicleGearLocation(vehicleId, gearId, string.Empty));
        Assert.Equal(string.Empty, character.Vehicles.Single().Children.Single().Location);
    }

    [Fact]
    public void ReloadWeapon_WorksForAVehicleMountedWeaponToo()
    {
        Guid vehicleId = Guid.NewGuid();
        CharacterDocument character = LoadXml("<character><nuyen>10000</nuyen><vehicles><vehicle>"
            + "<guid>" + vehicleId + "</guid><name>Americar</name><category>Cars</category><weapons />"
            + "</vehicle></vehicles></character>");
        character.AddVehicleMod(vehicleId, "Weapon Mount (Normal, External, Fixed, Manual)", "Standard", "0", "2",
            "8F", "1500", "SR4", "348");
        character.AddVehicleWeapon(vehicleId, "Ares Alpha", "Assault Rifles", "6P", "-1", "SA/BF/FA",
            "1", "42(c)", "2500", "12F", "SR4", "312");
        character.AddGear("Ammo: Regular Ammo", "Ammunition", strQty: "50", strCost: "0");

        CharacterTreeItemData weapon = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Guid guiWeaponId = Guid.Parse(weapon.ItemGuid);
        int intAmmoGearId = character.GetWeaponAmmoOptions(guiWeaponId).Single().GearId;

        Assert.True(character.ReloadWeapon(guiWeaponId, intAmmoGearId, 42));

        CharacterTreeItemData reloaded = character.Vehicles.Single().Children.Single(c => c.Name == "Ares Alpha");
        Assert.Equal("42 (Ammo: Regular Ammo)", reloaded.AmmoStatus);
        Assert.Equal("8", character.Gear.Single().Qty);
    }

}
