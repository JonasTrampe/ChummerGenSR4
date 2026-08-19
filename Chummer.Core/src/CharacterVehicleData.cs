using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace Chummer.Core
{
    public sealed class CharacterVehicleData
    {
        internal CharacterVehicleData(string strGuid, string strName, string strCategory, string strHandling, string strAcceleration,
            string strSpeed, string strPilot, string strBody, string strArmor, string strSensor,
            string strDeviceRating, string strAvail, string strCost, string strSlots, string strSource,
            string strPage, string strPhysicalCmFilled, string strNotes, bool blnIgnoreRules = false)
        {
            IgnoreRules = blnIgnoreRules;
            Guid = strGuid;
            Name = strName;
            Category = strCategory;
            Handling = strHandling;
            Acceleration = strAcceleration;
            Speed = strSpeed;
            Pilot = strPilot;
            Body = strBody;
            Armor = strArmor;
            Sensor = strSensor;
            DeviceRating = strDeviceRating;
            Avail = strAvail;
            Cost = strCost;
            Slots = strSlots;
            Source = strSource;
            Page = strPage;
            PhysicalCmFilled = strPhysicalCmFilled;
            Notes = strNotes;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Category { get; }
        public string Handling { get; }
        public string Acceleration { get; }
        public string Speed { get; }
        public string Pilot { get; }
        public string Body { get; }
        public string Armor { get; }
        public string Sensor { get; }
        public string DeviceRating { get; }
        public string Avail { get; }
        public string Cost { get; }
        public string Slots { get; }
        public string Source { get; }
        public string Page { get; }
        public string PhysicalCmFilled { get; }
        public string Notes { get; }
        public List<string> Locations { get; } = new();
        public List<CharacterTreeItemData> Children { get; } = new();
        private bool IgnoreRules { get; }

        /// <summary>Ported from clsEquipment.cs's Vehicle.Slots: 4 or the vehicle's Body, whichever
        /// is higher (the AddSlots-from-mods refinement isn't ported - no shipped vehicle mod
        /// grants bonus slots via a plain flat &lt;addslots&gt; value in the data this port reads).</summary>
        public int TotalSlots => Math.Max(4, int.TryParse(Body, out var b) ? b : 0);

        /// <summary>Ported from clsEquipment.cs's Vehicle.SlotsUsed: sums each installed,
        /// not-included-by-default Mod's Rating-evaluated Slots cost.</summary>
        public int SlotsUsed => Children
            .Where(c => c.IsVehicleMod && !c.IncludedInVehicle)
            .Sum(c => (int)RatingExpression.Evaluate(c.ModSlots, c.Rating));

        public int SlotsRemaining => TotalSlots - SlotsUsed;

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalCost: the vehicle's own cost, plus
        /// every non-included Mod's own (Body-and-Rating-resolved) cost, plus - for Mods that came
        /// included with the vehicle - the cost of any Weapon/Gear attached to them (their own slot
        /// cost doesn't count, but what's mounted on them still does), plus every other direct
        /// onboard Gear/Weapon's own cost.</summary>
        public int TotalCost
        {
            get
            {
                double dblBody = double.TryParse(Body, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 0;
                double dblTotal = double.TryParse(Cost, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : 0;
                foreach (CharacterTreeItemData objChild in Children)
                {
                    if (!objChild.IsVehicleMod)
                    {
                        dblTotal += objChild.CalculatedCost;
                    }
                    else if (objChild.IncludedInVehicle)
                    {
                        dblTotal += objChild.Children.Sum(objGrandchild => objGrandchild.CalculatedCost);
                    }
                    else
                    {
                        string strCost = objChild.Cost.Replace("Body",
                            dblBody.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                        dblTotal += RatingExpression.Evaluate(strCost, objChild.Rating);
                    }
                }

                return (int)Math.Ceiling(dblTotal);
            }
        }

        /// <summary>Ported from clsEquipment.cs's Vehicle.CalculatedSensor, faithfully including its
        /// "only ever looks at the first onboard Gear item" quirk (the loop's break is unconditional,
        /// outside the category check): averages the Rating of that first item's "Sensor Functions"
        /// category children (only if the first item is itself Category "Sensors" with a Signal
        /// value), rounded up; falls back to the saved Sensor value if there's no qualifying first
        /// item or it has no such children. Only used (see UseCalculatedVehicleSensorRatings) as an
        /// alternative to the saved value, never persisted over it.</summary>
        public int CalculatedSensor
        {
            get
            {
                CharacterTreeItemData? objFirst = Children.FirstOrDefault();
                if (objFirst is { Category: "Sensors" } && ParseInt(objFirst.Signal) > 0)
                {
                    var lstRatings = objFirst.Children.Where(c => c.Category == "Sensor Functions")
                        .Select(c => ParseInt(c.Rating)).Where(r => r > 0).ToList();
                    if (lstRatings.Count > 0)
                        return (int)Math.Ceiling(lstRatings.Average());
                }

                return ParseInt(Sensor);
            }
        }

        private static int ParseInt(string strValue) => int.TryParse(strValue, out var i) ? i : 0;

        /// <summary>Vehicle Mod bonus nodes are looked up by name from vehicles.xml's own
        /// &lt;mod&gt; rules-data entries - the saved character XML only keeps the mod's Name/
        /// Rating/installed state, not its rules-data bonus, mirroring the same pattern used for
        /// ArmorMod ballistic/impact bonuses elsewhere in this port.</summary>
        private static XmlNode? GetModBonusNode(CharacterTreeItemData objMod)
        {
            XmlDocument objVehiclesDoc = XmlManager.Instance.Load("vehicles.xml");
            return objVehiclesDoc.SelectSingleNode($"/chummer/mods/mod[name = '{objMod.Name}']")
                ?.SelectSingleNode("bonus");
        }

        private IEnumerable<CharacterTreeItemData> ActiveMods =>
            Children.Where(c => c.IsVehicleMod && !c.IncludedInVehicle && c.Equipped);

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalBody: base Body plus each active
        /// Mod's own flat &lt;body&gt; bonus.</summary>
        public int TotalBody => ParseInt(Body) + ActiveMods.Sum(objMod =>
            ParseInt(GetModBonusNode(objMod)?["body"]?.InnerText ?? string.Empty));

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalHandling: base Handling plus each
        /// active Mod's own flat &lt;handling&gt; bonus.</summary>
        public int TotalHandling => ParseInt(Handling) + ActiveMods.Sum(objMod =>
            ParseInt(GetModBonusNode(objMod)?["handling"]?.InnerText ?? string.Empty));

        /// <summary>Ported from clsEquipment.cs's Vehicle.MaxArmor: Body x2 (x3 for Drones), or
        /// unlimited (20) under the IgnoreRules house rule.</summary>
        public int MaxArmor => IgnoreRules ? 20 : TotalBody * (Category.StartsWith("Drones:", StringComparison.Ordinal) ? 3 : 2);

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalArmor: Mod &lt;armor&gt; bonuses
        /// (each capped individually at MaxArmor, with "Rating" resolved to the Mod's own Rating)
        /// entirely replace the Vehicle's base Armor rather than adding to it, once any such Mod is
        /// present - matching legacy's own asymmetry with Body/Handling, which simply add.</summary>
        public int TotalArmor
        {
            get
            {
                int intBaseArmor = ParseInt(Armor);
                int intModArmor = 0;
                bool blnHasArmorMod = false;
                foreach (CharacterTreeItemData objMod in ActiveMods)
                {
                    string strArmorBonus = GetModBonusNode(objMod)?["armor"]?.InnerText ?? string.Empty;
                    if (string.IsNullOrEmpty(strArmorBonus))
                        continue;
                    blnHasArmorMod = true;
                    intModArmor += Math.Min(MaxArmor, (int)RatingExpression.Evaluate(strArmorBonus, objMod.Rating));
                }

                return (blnHasArmorMod ? 0 : intBaseArmor) + intModArmor;
            }
        }

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalSpeed: base Speed, plus base Speed x
        /// each active Mod's own &lt;speed&gt; multiplier, minus 20% if Total Armor exceeds both
        /// Total Body and the Vehicle's own base Armor rating (overburdened), floored at 0 and
        /// rounded up.</summary>
        public int TotalSpeed
        {
            get
            {
                int intBaseSpeed = ParseInt(Speed);
                decimal decSpeed = intBaseSpeed;
                foreach (CharacterTreeItemData objMod in ActiveMods)
                {
                    string strSpeedBonus = GetModBonusNode(objMod)?["speed"]?.InnerText ?? string.Empty;
                    if (string.IsNullOrEmpty(strSpeedBonus))
                        continue;
                    if (decimal.TryParse(strSpeedBonus, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        decSpeed += intBaseSpeed * d;
                }

                if (TotalArmor > TotalBody && TotalArmor > ParseInt(Armor))
                    decSpeed -= intBaseSpeed * 0.2m;

                return (int)Math.Ceiling(Math.Max(0m, decSpeed));
            }
        }

        /// <summary>Ported from clsEquipment.cs's Vehicle.TotalAccel: base walking/running
        /// Acceleration (from the saved "walking/running" Acceleration string), each independently
        /// adjusted by active Mods' own &lt;accel&gt; bonus - either a flat "+x/y" pair (with
        /// "Rating" resolved to the Mod's own Rating) or, without a +/- sign, a multiplier applied
        /// to the base value - then reduced 20% if overburdened per TotalSpeed's same armor check,
        /// floored at 0 and rounded up.</summary>
        public string TotalAccel
        {
            get
            {
                string[] astrBase = Acceleration.Split('/');
                int intBaseWalking = astrBase.Length > 0 ? ParseInt(astrBase[0]) : 0;
                int intBaseRunning = astrBase.Length > 1 ? ParseInt(astrBase[1]) : 0;
                decimal decWalking = intBaseWalking;
                decimal decRunning = intBaseRunning;

                foreach (CharacterTreeItemData objMod in ActiveMods)
                {
                    string strAccelBonus = GetModBonusNode(objMod)?["accel"]?.InnerText ?? string.Empty;
                    if (string.IsNullOrEmpty(strAccelBonus))
                        continue;

                    if (strAccelBonus.Contains('+') || strAccelBonus.Contains('-'))
                    {
                        string[] astrBonus = strAccelBonus.Split('/');
                        decWalking += (decimal)RatingExpression.Evaluate(astrBonus[0].Replace("+", string.Empty), objMod.Rating);
                        if (astrBonus.Length > 1)
                            decRunning += (decimal)RatingExpression.Evaluate(astrBonus[1].Replace("+", string.Empty), objMod.Rating);
                    }
                    else if (decimal.TryParse(strAccelBonus, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    {
                        decWalking += intBaseWalking * d;
                        decRunning += intBaseRunning * d;
                    }
                }

                if (TotalArmor > TotalBody && TotalArmor > ParseInt(Armor))
                {
                    decWalking -= intBaseWalking * 0.2m;
                    decRunning -= intBaseRunning * 0.2m;
                }

                int intWalking = (int)Math.Ceiling(Math.Max(0m, decWalking));
                int intRunning = (int)Math.Ceiling(Math.Max(0m, decRunning));
                return $"{intWalking}/{intRunning}";
            }
        }

        /// <summary>What the UI/print export should actually show for Sensor - <see cref="Sensor"/>
        /// (the saved value) unless UseCalculatedVehicleSensorRatings is on, in which case it's
        /// <see cref="CalculatedSensor"/>. Set once by ReadVehicles, since only it has access to the
        /// character's settings profile.</summary>
        public string SensorDisplay { get; private set; } = string.Empty;

        internal void SetSensorDisplay(string strValue) => SensorDisplay = strValue;
    }

}
