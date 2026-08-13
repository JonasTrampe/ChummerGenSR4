using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace Chummer.Core
{
    /// <summary>
    ///     Builds the "print XML" a character sheet XSLT (Chummer.Core/data/sheets/*.xsl) transforms
    ///     into a rendered sheet - ported from clsCharacter.cs's PrintToStream, matching the field
    ///     names/shape Text-Only.xsl expects (the simplest of the shipped sheets and the first one
    ///     this port targets). Deliberately scoped to the sections a typical character actually uses:
    ///     Info, Attributes, derived stats, Skills, Contacts, Qualities, Spells, Adept Powers,
    ///     Complex Forms, Critter Powers, Martial Arts, Lifestyles, Cyberware/Bioware, Gear (incl.
    ///     Commlinks), Armor, Weapons (incl. dice pool), and Vehicles (mods/gear/weapons, though
    ///     vehicle-mounted Weapons don't carry damage/AP/RC in this port's saved tree data yet).
    /// </summary>
    public static class CharacterSheetExporter
    {
        public static XmlDocument BuildExportXml(CharacterDocument character)
        {
            var doc = new XmlDocument();
            XmlElement root = doc.CreateElement("characters");
            doc.AppendChild(root);
            XmlElement charEl = doc.CreateElement("character");
            root.AppendChild(charEl);

            AppendInfo(doc, charEl, character);
            AppendAttributes(doc, charEl, character);
            AppendDerived(doc, charEl, character);
            AppendSkills(doc, charEl, character);
            AppendContacts(doc, charEl, character);
            AppendQualities(doc, charEl, character);
            AppendSpells(doc, charEl, character);
            AppendPowers(doc, charEl, character);
            AppendComplexForms(doc, charEl, character);
            AppendCritterPowers(doc, charEl, character);
            AppendMartialArts(doc, charEl, character);
            AppendLifestyles(doc, charEl, character);
            AppendCyberware(doc, charEl, character);
            AppendGear(doc, charEl, character);
            AppendArmor(doc, charEl, character);
            AppendWeapons(doc, charEl, character);
            AppendVehicles(doc, charEl, character);
            AppendExpenses(doc, charEl, character);

            return doc;
        }

        /// <summary>Loads a sheet by filename from Chummer.Core/data/sheets (matching XmlManager's
        /// data-file lookup convention) and transforms this character's export XML through it.
        /// System.Xml.Xsl.XslCompiledTransform is a first-party .NET API and works cross-platform,
        /// unlike the legacy app's reliance on a Windows-only WebBrowser control to render the
        /// result - this returns the raw HTML string for a host UI to display however it likes.</summary>
        public static string RenderSheet(CharacterDocument character, string strSheetFileName)
        {
            string strSheetPath = Path.Combine(AppContext.BaseDirectory, "data", "sheets", strSheetFileName);
            if (!File.Exists(strSheetPath))
                throw new FileNotFoundException("Character sheet not found.", strSheetPath);

            // Several shipped sheets (Shadowrun 4.xsl, the "Grouped Skills" variants) xsl:include
            // a shared base stylesheet from the same folder - XmlReader.Create's default resolver
            // refuses that as an "external URI" under .NET's hardened-by-default settings, so load
            // via the path overload with an explicit resolver instead.
            var transform = new XslCompiledTransform();
            transform.Load(strSheetPath, XsltSettings.TrustedXslt, new XmlUrlResolver());

            XmlDocument exportXml = BuildExportXml(character);
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            using (var xmlWriter = XmlWriter.Create(writer, transform.OutputSettings))
                transform.Transform(exportXml, xmlWriter);

            return sb.ToString();
        }

        private static XmlElement AddEl(XmlDocument doc, XmlElement parent, string name, string value = "")
        {
            XmlElement el = doc.CreateElement(name);
            el.InnerText = value ?? string.Empty;
            parent.AppendChild(el);
            return el;
        }

        private static void AppendInfo(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            AddEl(doc, charEl, "name", c.Name);
            AddEl(doc, charEl, "alias", c.Alias);
            AddEl(doc, charEl, "movement", c.WalkMovement);
            AddEl(doc, charEl, "totalkarma", c.CareerKarma.ToString());
            AddEl(doc, charEl, "totalstreetcred", c.StreetCred);
            AddEl(doc, charEl, "totalnotoriety", c.Notoriety);
            AddEl(doc, charEl, "totalpublicawareness", c.PublicAwareness);
            AddEl(doc, charEl, "metatype", c.Metatype);
            AddEl(doc, charEl, "metavariant", string.Empty);
            AddEl(doc, charEl, "sex", c.Gender);
            AddEl(doc, charEl, "age", string.Empty);
            AddEl(doc, charEl, "height", c.Height);
            AddEl(doc, charEl, "weight", c.Weight);
            AddEl(doc, charEl, "composure", c.Composure.Value.ToString());
            AddEl(doc, charEl, "judgeintentions", c.JudgeIntentions.Value.ToString());
            AddEl(doc, charEl, "liftandcarry", c.LiftAndCarry.Value.ToString());
            AddEl(doc, charEl, "liftweight", string.Empty);
            AddEl(doc, charEl, "carryweight", string.Empty);
            AddEl(doc, charEl, "memory", c.Memory.Value.ToString());
            AddEl(doc, charEl, "nuyen", c.Nuyen);
            AddEl(doc, charEl, "magenabled", (c.Magician || c.Adept).ToString());
            AddEl(doc, charEl, "resenabled", c.Technomancer.ToString());
            AddEl(doc, charEl, "tradition", string.Empty);
            AddEl(doc, charEl, "drain", string.Empty);
            // Ported from clsCharacter.cs's PrintToStream: the PrintNotes house rule also gates
            // Contact/MartialArtManeuver notes there, but this port's shipped sheets (Text-Only.xsl)
            // only ever read the character's own general notes field, so that's the only notes
            // output this house rule needs to control here.
            AddEl(doc, charEl, "notes", c.PrintNotesEnabled ? c.Notes : string.Empty);
        }

        private static void AppendAttributes(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement attributesEl = doc.CreateElement("attributes");
            charEl.AppendChild(attributesEl);
            foreach (CharacterAttributeData attr in c.Attributes)
            {
                XmlElement attrEl = doc.CreateElement("attribute");
                attributesEl.AppendChild(attrEl);
                AddEl(doc, attrEl, "name", attr.Code);
                AddEl(doc, attrEl, "base", attr.TotalValue);
                AddEl(doc, attrEl, "total", attr.Augmented.Value.ToString());
            }
        }

        private static void AppendDerived(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            AddPair(doc, charEl, "init", c.Initiative.Base, c.Initiative.Augmented);
            AddPair(doc, charEl, "ip", c.InitiativePasses.Base, c.InitiativePasses.Augmented);
            if (c.Awakened)
                AddPair(doc, charEl, "astralinit", c.AstralInitiative.Base, c.AstralInitiative.Augmented);
            if (c.Technomancer)
                AddPair(doc, charEl, "matrixinit", c.MatrixInitiative.Base, c.MatrixInitiative.Augmented);
            AddEl(doc, charEl, "physicalcm", c.Condition.PhysicalCm.Value.ToString());
            AddEl(doc, charEl, "stuncm", c.Condition.StunCm.Value.ToString());
        }

        private static void AddPair(XmlDocument doc, XmlElement charEl, string name, int intBase, int intAugmented)
        {
            XmlElement el = doc.CreateElement(name);
            charEl.AppendChild(el);
            AddEl(doc, el, "base", intBase.ToString());
            AddEl(doc, el, "total", intAugmented.ToString());
        }

        private static void AppendSkills(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement skillsEl = doc.CreateElement("skills");
            charEl.AppendChild(skillsEl);

            // Ported from clsCharacter.cs's PrintToStream: PrintLeadershipAlternates/
            // PrintArcanaAlternates staple a couple of synthetic same-rating copies of Leadership/
            // Arcana onto the printed skill list, each linked to a different Attribute.
            var lstActiveSkills = new List<CharacterSkillData>(c.Skills);
            if (c.PrintLeadershipAlternates)
            {
                CharacterSkillData? leadership = lstActiveSkills.FirstOrDefault(s => s.Name == "Leadership");
                if (leadership != null)
                {
                    lstActiveSkills.Add(c.BuildAlternateSkillForPrint(leadership, "Command", "LOG"));
                    lstActiveSkills.Add(c.BuildAlternateSkillForPrint(leadership, "Direct Fire", "INT"));
                }
            }
            if (c.PrintArcanaAlternates)
            {
                CharacterSkillData? arcana = lstActiveSkills.FirstOrDefault(s => s.Name == "Arcana");
                if (arcana != null)
                {
                    lstActiveSkills.Add(c.BuildAlternateSkillForPrint(arcana, "Metamagic", "INT"));
                    lstActiveSkills.Add(c.BuildAlternateSkillForPrint(arcana, "Artificing", "MAG"));
                }
            }

            IEnumerable<CharacterSkillData> lstPrintedSkills = lstActiveSkills.Concat(c.KnowledgeSkills);
            if (!c.PrintSkillsWithZeroRating)
            {
                lstPrintedSkills = lstPrintedSkills.Where(skill =>
                    skill.KnowledgeSkill || (int.TryParse(skill.BaseRating, out var intRating) && intRating > 0));
            }

            foreach (CharacterSkillData skill in lstPrintedSkills)
            {
                XmlElement skillEl = doc.CreateElement("skill");
                skillsEl.AppendChild(skillEl);
                AddEl(doc, skillEl, "name", skill.Name);
                AddEl(doc, skillEl, "rating", skill.BaseRating);
                AddEl(doc, skillEl, "spec", skill.Specialization);
                AddEl(doc, skillEl, "total", skill.TotalValue);
                AddEl(doc, skillEl, "knowledge", skill.KnowledgeSkill.ToString());
                AddEl(doc, skillEl, "exotic", skill.Exotic.ToString());
                AddEl(doc, skillEl, "islanguage", (skill.Category == "Language").ToString());
            }
        }

        private static void AppendContacts(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement contactsEl = doc.CreateElement("contacts");
            charEl.AppendChild(contactsEl);
            foreach (CharacterContactData contact in c.Contacts)
            {
                XmlElement contactEl = doc.CreateElement("contact");
                contactsEl.AppendChild(contactEl);
                AddEl(doc, contactEl, "name", contact.Name);
                AddEl(doc, contactEl, "connection", contact.Connection);
                AddEl(doc, contactEl, "loyalty", contact.Loyalty);
            }
        }

        private static void AppendQualities(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement qualitiesEl = doc.CreateElement("qualities");
            charEl.AppendChild(qualitiesEl);
            foreach (CharacterQualityData quality in c.Qualities)
            {
                XmlElement qualityEl = doc.CreateElement("quality");
                qualitiesEl.AppendChild(qualityEl);
                AddEl(doc, qualityEl, "name", quality.Name);
                AddEl(doc, qualityEl, "extra", quality.Extra);
            }
        }

        private static void AppendSpells(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement spellsEl = doc.CreateElement("spells");
            charEl.AppendChild(spellsEl);
            foreach (CharacterSpellData spell in c.Spells)
            {
                XmlElement spellEl = doc.CreateElement("spell");
                spellsEl.AppendChild(spellEl);
                AddEl(doc, spellEl, "name", spell.Name);
                AddEl(doc, spellEl, "extra", string.Empty);
                AddEl(doc, spellEl, "dv", spell.Dv);
            }
        }

        private static void AppendPowers(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement powersEl = doc.CreateElement("powers");
            charEl.AppendChild(powersEl);
            foreach (CharacterPowerData power in c.AdeptPowers)
            {
                XmlElement powerEl = doc.CreateElement("power");
                powersEl.AppendChild(powerEl);
                AddEl(doc, powerEl, "name", power.Name);
                AddEl(doc, powerEl, "extra", power.Extra);
                AddEl(doc, powerEl, "rating", power.Rating);
            }
        }

        private static void AppendComplexForms(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement formsEl = doc.CreateElement("techprograms");
            charEl.AppendChild(formsEl);
            foreach (CharacterComplexFormData form in c.ComplexForms)
            {
                XmlElement formEl = doc.CreateElement("techprogram");
                formsEl.AppendChild(formEl);
                AddEl(doc, formEl, "name", form.Name);
                AddEl(doc, formEl, "extra", form.Extra);
                AddEl(doc, formEl, "rating", form.Rating);
                XmlElement optionsEl = doc.CreateElement("programoptions");
                formEl.AppendChild(optionsEl);
                foreach ((string strOptionName, string strOptionRating) in form.Options)
                {
                    XmlElement optionEl = doc.CreateElement("programoption");
                    optionsEl.AppendChild(optionEl);
                    AddEl(doc, optionEl, "name", strOptionName);
                    AddEl(doc, optionEl, "rating", strOptionRating);
                }
            }
        }

        private static void AppendCritterPowers(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement powersEl = doc.CreateElement("critterpowers");
            charEl.AppendChild(powersEl);
            foreach (CharacterCritterPowerData power in c.CritterPowers)
            {
                XmlElement powerEl = doc.CreateElement("critterpower");
                powersEl.AppendChild(powerEl);
                AddEl(doc, powerEl, "name", power.Name);
                AddEl(doc, powerEl, "extra", power.Extra);
                AddEl(doc, powerEl, "rating", power.Points);
            }
        }

        private static void AppendMartialArts(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement martialArtsEl = doc.CreateElement("martialarts");
            charEl.AppendChild(martialArtsEl);
            foreach (CharacterMartialArtData martialArt in c.MartialArts)
            {
                XmlElement maEl = doc.CreateElement("martialart");
                martialArtsEl.AppendChild(maEl);
                AddEl(doc, maEl, "name", martialArt.Name);
                XmlElement advantagesEl = doc.CreateElement("martialartadvantages");
                maEl.AppendChild(advantagesEl);
                foreach (string advantage in martialArt.Advantages)
                    AddEl(doc, advantagesEl, "martialartadvantage", advantage);
            }

            XmlElement maneuversEl = doc.CreateElement("martialartmaneuvers");
            charEl.AppendChild(maneuversEl);
            foreach (CharacterMartialArtManeuverData maneuver in c.MartialArtManeuvers)
            {
                XmlElement maneuverEl = doc.CreateElement("martialartmaneuver");
                maneuversEl.AppendChild(maneuverEl);
                AddEl(doc, maneuverEl, "name", maneuver.Name);
            }
        }

        private static void AppendLifestyles(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement lifestylesEl = doc.CreateElement("lifestyles");
            charEl.AppendChild(lifestylesEl);
            foreach (CharacterLifestyleData lifestyle in c.Lifestyles)
            {
                XmlElement lifestyleEl = doc.CreateElement("lifestyle");
                lifestylesEl.AppendChild(lifestyleEl);
                AddEl(doc, lifestyleEl, "name", lifestyle.Name);
                AddEl(doc, lifestyleEl, "lifestylename", string.Empty);
                AddEl(doc, lifestyleEl, "months", lifestyle.Months);
            }
        }

        private static void AppendCyberware(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement cyberwaresEl = doc.CreateElement("cyberwares");
            charEl.AppendChild(cyberwaresEl);
            foreach (CharacterTreeItemData item in c.Cyberware.Concat(c.Bioware))
                AppendGearLikeItem(doc, cyberwaresEl, "cyberware", item, includeChildren: true);
        }

        private static void AppendGear(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement gearsEl = doc.CreateElement("gears");
            charEl.AppendChild(gearsEl);
            foreach (CharacterTreeItemData item in c.Gear)
                AppendGearLikeItem(doc, gearsEl, "gear", item, includeChildren: true);
        }

        private static void AppendGearLikeItem(XmlDocument doc, XmlElement parent, string elementName,
            CharacterTreeItemData item, bool includeChildren)
        {
            XmlElement itemEl = doc.CreateElement(elementName);
            parent.AppendChild(itemEl);
            AddEl(doc, itemEl, "name", item.TranslatedName);
            AddEl(doc, itemEl, "extra", string.Empty);
            AddEl(doc, itemEl, "rating", item.Rating);
            AddEl(doc, itemEl, "qty", item.Qty);
            AddEl(doc, itemEl, "location", item.Location);
            AddEl(doc, itemEl, "iscommlink", item.HasCommlinkStats.ToString());
            if (item.HasCommlinkStats)
            {
                AddEl(doc, itemEl, "response", item.EffectiveResponse);
                AddEl(doc, itemEl, "system", item.EffectiveSystem);
                AddEl(doc, itemEl, "firewall", item.EffectiveFirewall);
                AddEl(doc, itemEl, "signal", item.EffectiveSignal);
            }

            if (includeChildren && item.Children.Count > 0)
            {
                XmlElement childrenEl = doc.CreateElement("children");
                itemEl.AppendChild(childrenEl);
                foreach (CharacterTreeItemData child in item.Children)
                    AppendGearLikeItem(doc, childrenEl, elementName == "cyberware" ? "cyberware" : "gear", child, includeChildren: true);
            }
        }

        private static void AppendArmor(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement armorsEl = doc.CreateElement("armors");
            charEl.AppendChild(armorsEl);
            foreach (CharacterTreeItemData item in c.Armor)
            {
                XmlElement armorEl = doc.CreateElement("armor");
                armorsEl.AppendChild(armorEl);
                AddEl(doc, armorEl, "name", item.TranslatedName);
                AddEl(doc, armorEl, "armorname", string.Empty);
                AddEl(doc, armorEl, "b", item.Ballistic);
                AddEl(doc, armorEl, "i", item.Impact);
            }
        }

        private static void AppendWeapons(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement weaponsEl = doc.CreateElement("weapons");
            charEl.AppendChild(weaponsEl);
            foreach (CharacterWeaponData weapon in c.Weapons)
            {
                XmlElement weaponEl = doc.CreateElement("weapon");
                weaponsEl.AppendChild(weaponEl);
                AddEl(doc, weaponEl, "name", weapon.Name);
                AddEl(doc, weaponEl, "weaponname", string.Empty);
                AddEl(doc, weaponEl, "dicepool", weapon.DicePool);
                AddEl(doc, weaponEl, "damage", weapon.Damage);
                AddEl(doc, weaponEl, "ap", weapon.Ap);
                AddEl(doc, weaponEl, "rc", weapon.Rc);
                weaponEl.AppendChild(doc.CreateElement("accessories"));
                weaponEl.AppendChild(doc.CreateElement("mods"));
            }
        }

        private static void AppendVehicles(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement vehiclesEl = doc.CreateElement("vehicles");
            charEl.AppendChild(vehiclesEl);
            foreach (CharacterVehicleData vehicle in c.Vehicles)
            {
                XmlElement vehicleEl = doc.CreateElement("vehicle");
                vehiclesEl.AppendChild(vehicleEl);
                AddEl(doc, vehicleEl, "name", vehicle.Name);
                AddEl(doc, vehicleEl, "vehiclename", string.Empty);

                XmlElement modsEl = doc.CreateElement("mods");
                vehicleEl.AppendChild(modsEl);
                XmlElement gearsEl = doc.CreateElement("gears");
                vehicleEl.AppendChild(gearsEl);
                XmlElement weaponsEl = doc.CreateElement("weapons");
                vehicleEl.AppendChild(weaponsEl);

                foreach (CharacterTreeItemData item in vehicle.Children)
                {
                    if (item.IsVehicleMod)
                    {
                        XmlElement modEl = doc.CreateElement("mod");
                        modsEl.AppendChild(modEl);
                        AddEl(doc, modEl, "name", item.TranslatedName);
                        AddEl(doc, modEl, "rating", item.Rating);
                        modEl.AppendChild(doc.CreateElement("cyberwares"));
                    }
                    else if (item.IsVehicleWeapon)
                    {
                        XmlElement weaponEl = doc.CreateElement("weapon");
                        weaponsEl.AppendChild(weaponEl);
                        AddEl(doc, weaponEl, "name", item.TranslatedName);
                        AddEl(doc, weaponEl, "weaponname", string.Empty);
                        // Vehicle-mounted weapons don't carry damage/AP/RC in this port's saved
                        // tree data (only name/rating/cost/avail) - left blank rather than wrong.
                        AddEl(doc, weaponEl, "damage", string.Empty);
                        AddEl(doc, weaponEl, "ap", string.Empty);
                        AddEl(doc, weaponEl, "rc", string.Empty);
                        weaponEl.AppendChild(doc.CreateElement("accessories"));
                        weaponEl.AppendChild(doc.CreateElement("mods"));
                    }
                    else
                    {
                        AppendGearLikeItem(doc, gearsEl, "gear", item, includeChildren: true);
                    }
                }
            }
        }

        private static void AppendExpenses(XmlDocument doc, XmlElement charEl, CharacterDocument c)
        {
            XmlElement expensesEl = doc.CreateElement("expenses");
            charEl.AppendChild(expensesEl);
            if (!c.PrintExpenses)
                return;

            foreach (CharacterExpenseData expense in c.KarmaExpenses)
                AppendExpense(doc, expensesEl, expense, "Karma");
            foreach (CharacterExpenseData expense in c.NuyenExpenses)
                AppendExpense(doc, expensesEl, expense, "Nuyen");
        }

        private static void AppendExpense(XmlDocument doc, XmlElement parent, CharacterExpenseData expense, string type)
        {
            XmlElement expenseEl = doc.CreateElement("expense");
            parent.AppendChild(expenseEl);
            AddEl(doc, expenseEl, "date", expense.DisplayDate);
            AddEl(doc, expenseEl, "amount", expense.Amount);
            AddEl(doc, expenseEl, "reason", expense.Reason);
            AddEl(doc, expenseEl, "type", type);
        }
    }
}
