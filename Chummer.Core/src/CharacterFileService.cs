using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Chummer.Tests")]

namespace Chummer.Core
{
    /// <summary>
    ///     Platform-neutral reader and writer for the existing Chummer character-file format.
    ///     This is deliberately a small extraction from <c>Character</c>: callers can display and
    ///     round-trip a save before the complete legacy domain object has been moved into Core.
    /// </summary>
    public sealed class CharacterFileService
    {
        public CharacterDocument Load(Stream objStream, string strSourceName)
        {
            if (objStream == null) throw new ArgumentNullException(nameof(objStream));
            Trace.TraceInformation("Loading Chummer character from {0}", strSourceName);
            var objStopwatch = Stopwatch.StartNew();
            try
            {
                var objDocument = new XmlDocument();
                objDocument.Load(objStream);
                if (objDocument.DocumentElement == null || objDocument.DocumentElement.Name != "character")
                    throw new InvalidDataException("The selected file is not a Chummer character document.");
                var lngXmlParseMs = objStopwatch.ElapsedMilliseconds;

                var objCharacter = new CharacterDocument(objDocument, strSourceName);
                objStopwatch.Stop();
                Trace.TraceInformation(
                    "Loaded Chummer character {0} from {1} (XML parse: {2}ms, total: {3}ms)",
                    objCharacter.DisplayName, strSourceName, lngXmlParseMs, objStopwatch.ElapsedMilliseconds);
                return objCharacter;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to load Chummer character from {0}: {1}", strSourceName, ex);
                throw;
            }
        }

        public void Save(CharacterDocument objCharacter, Stream objStream, string strTargetName)
        {
            if (objCharacter == null) throw new ArgumentNullException(nameof(objCharacter));
            if (objStream == null) throw new ArgumentNullException(nameof(objStream));
            Trace.TraceInformation("Saving Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
            try
            {
                // Persist a usable timestamp for calendar/history consumers. Future Shadowrun
                // dates are intentionally preserved; stale dates are refreshed only when the
                // user-selected policy allows it.
                if (objCharacter.LastDate == DateTime.MinValue
                    || (GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder
                        && objCharacter.LastDate < DateTime.Now))
                    objCharacter.LastDate = DateTime.Now;
                // Match legacy save formatting (tab indent, UTF-16, CRLF - XmlWriterSettings
                // defaults NewLineChars to Environment.NewLine, which is LF on Linux and would
                // make every re-save of an untouched Windows-authored file diff as "changed").
                // CloseOutput=false since callers read the stream back after Save() returns.
                var objSettings = new XmlWriterSettings
                {
                    Encoding = Encoding.Unicode,
                    Indent = true,
                    IndentChars = "\t",
                    NewLineChars = "\r\n",
                    CloseOutput = false
                };
                using (var objWriter = XmlWriter.Create(objStream, objSettings))
                {
                    objCharacter.Document.Save(objWriter);
                }

                Trace.TraceInformation("Saved Chummer character {0} to {1}", objCharacter.DisplayName, strTargetName);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to save Chummer character {0} to {1}: {2}", objCharacter.DisplayName,
                    strTargetName, ex);
                throw;
            }
        }

        /// <summary>Creates the legacy Create-Mode snapshot before a character enters career
        /// mode. The cross-platform destination is a <c>backup</c> subdirectory beside the saved
        /// character, rather than the legacy application's installation directory. A temporary
        /// file plus replace/move avoids leaving a partial backup after an interrupted write.</summary>
        public string? CreateCareerBackup(CharacterDocument objCharacter, string? strSourcePath)
        {
            if (objCharacter == null) throw new ArgumentNullException(nameof(objCharacter));
            if (!objCharacter.CreateBackupOnCareerEnabled || objCharacter.Created
                || string.IsNullOrWhiteSpace(strSourcePath))
                return null;

            string? strDirectory = Path.GetDirectoryName(strSourcePath);
            if (string.IsNullOrWhiteSpace(strDirectory))
                return null;

            string strBaseName = Path.GetFileNameWithoutExtension(strSourcePath);
            if (string.IsNullOrWhiteSpace(strBaseName))
                strBaseName = string.IsNullOrWhiteSpace(objCharacter.Alias) ? objCharacter.Name : objCharacter.Alias;
            if (string.IsNullOrWhiteSpace(strBaseName))
                strBaseName = Guid.NewGuid().ToString("N")[..13];

            string strBackupDirectory = Path.Combine(strDirectory, "backup");
            Directory.CreateDirectory(strBackupDirectory);
            string strBackupPath = Path.Combine(strBackupDirectory, strBaseName + " (Create Mode).chum");
            string strTemporaryPath = strBackupPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream objStream = File.Create(strTemporaryPath))
                    Save(objCharacter, objStream, Path.GetFileName(strBackupPath));
                File.Move(strTemporaryPath, strBackupPath, true);
                Trace.TraceInformation("Created pre-career Chummer backup at {0}", strBackupPath);
                return strBackupPath;
            }
            finally
            {
                if (File.Exists(strTemporaryPath))
                    File.Delete(strTemporaryPath);
            }
        }
    }

    public sealed partial class CharacterDocument
    {
        internal CharacterDocument(XmlDocument objDocument, string strDisplayName)
        {
            Document = objDocument;
            DisplayName = strDisplayName;
            // Every Read*()-backed collection property (Improvements, Skills, Qualities, Gear,
            // Attributes, ...) is cached per instance instead of rebuilt from XML on every access -
            // subscribing here catches every Changed?.Invoke() call site (100+ across
            // CharacterFileService.*.cs) rather than needing each of them to also clear these
            // caches directly.
            Changed += InvalidateReadCaches;
        }

        /// <summary>Clears every Read*()-backed collection cache - see the constructor's Changed
        /// subscription. One list here rather than scattering the invalidation across every
        /// mutator, and easier to audit for completeness than remembering to update N call sites
        /// whenever a new cached collection is added.</summary>
        private void InvalidateReadCaches()
        {
            _lstCachedImprovements = null;
            _cachedCritterPowers = null;
            _cachedMetamagics = null;
            _cachedAdeptPowers = null;
            _cachedCalendar = null;
            _cachedAttributes = null;
            _cachedCommlinks = null;
            _cachedSpirits = null;
            _cachedFoci = null;
            _cachedStackedFoci = null;
            _cachedVehicles = null;
            _cachedPets = null;
            _cachedMartialArts = null;
            _cachedMartialArtManeuvers = null;
            _cachedQualities = null;
            _cachedLifestyles = null;
            _cachedInitiationGrades = null;
            _cachedSpells = null;
            _cachedArmor = null;
            _cachedSkillGroups = null;
            _cachedSkills = null;
            _cachedKnowledgeSkills = null;
            _cachedComplexForms = null;
            _cachedGear = null;
            _cachedWeapons = null;
            _cachedWeaponTrees = null;
        }

        internal XmlDocument Document { get; }
        public string DisplayName { get; }

        public string Name => GetValue("/character/name", DisplayName);

        /// <summary>Timestamp persisted in the character file for calendar and history operations.</summary>
        public DateTime LastDate
        {
            get => DateTime.TryParse(GetValue("/character/lastdate", string.Empty), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime datValue) ? datValue : DateTime.MinValue;
            set => SetRootValue("lastdate", value.ToString("O", CultureInfo.InvariantCulture));
        }

        /// <summary>Returns the timestamp to use for date-sensitive operations.</summary>
        public DateTime EffectiveLastDate
            => GlobalOptions.Instance.UseCurrentDateWhenLastDateIsOlder && LastDate < DateTime.Now
                ? DateTime.Now : LastDate;

        public string Alias
        {
            get => GetValue("/character/alias", string.Empty);
            set => SetRootValue("alias", value);
        }

        public string CloudDocumentId
        {
            get => GetValue("/character/clouddocumentid", string.Empty);
            set => SetRootValue("clouddocumentid", value);
        }

        public string CloudLastKnownRevisionId
        {
            get => GetValue("/character/cloudlastknownrevisionid", string.Empty);
            set => SetRootValue("cloudlastknownrevisionid", value);
        }

        public bool CloudIsShared
        {
            get => GetValue("/character/cloudisshared", "False") == "True";
            set => SetRootValue("cloudisshared", value ? "True" : "False");
        }

        public string CloudMetadataDisplayName
        {
            get => GetValue("/character/clouddisplayname", string.Empty);
            set => SetRootValue("clouddisplayname", value);
        }

        public string CloudMetadataDescription
        {
            get => GetValue("/character/clouddescription", string.Empty);
            set => SetRootValue("clouddescription", value);
        }

        public string CloudMetadataImageUrl
        {
            get => GetValue("/character/cloudimageurl", string.Empty);
            set => SetRootValue("cloudimageurl", value);
        }

        public string Metavariant => GetValue("/character/metavariant", string.Empty);

        public bool IsCritter => GetValue("/character/critter", "False") == "True";

        public bool Adept => GetValue("/character/adept", "False") == "True";

        public bool Magician => GetValue("/character/magician", "False") == "True";

        public bool MysticAdept => Adept && Magician;

        public bool Awakened => Adept || Magician;

        public int MysticAdeptAdeptMagSplit =>
            int.TryParse(GetValue("/character/magsplitadept", "0"), out var i) ? i : 0;

        public bool Technomancer => GetValue("/character/technomancer", "False") == "True";

        /// <summary>Whether this character may select an Autosoft as a Complex Form under the
        /// active settings profile's Technomancer house rule.</summary>
        public bool TechnomancerAllowsAutosoft => Technomancer && GetCharacterOptions().TechnomancerAllowAutosoft;

        /// <summary>Whether the optional Street Magic rule allowing every Detection spell to be
        /// acquired in an Extended form is active for this character.</summary>
        public bool AllowObsolescentUpgradeEnabled => GetCharacterOptions().AllowObsolescentUpgrade;

        /// <summary>Whether skill dice pools can be sent directly to the dice roller.</summary>
        public bool CreateBackupOnCareerEnabled => GetCharacterOptions().CreateBackupOnCareer;

        /// <summary>Whether destructive UI actions should ask for confirmation, matching the
        /// per-character settings profile's ConfirmDelete option.</summary>
        public string Bp
        {
            get => GetValue("/character/bp", "0");
            set => SetRootValue("bp", value);
        }

        /// <summary>Core counterpart to frmSelectBP when opened from Special/Change BP/Avail
        /// Limit. Replaces the creation-pool configuration as one operation; it deliberately
        /// does not attempt to recalculate already-spent categories.</summary>
        private static int ParseInteger(string strValue) => int.TryParse(strValue, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int intValue) ? intValue : 0;

        private static string DescribeSource(string strSource)
        {
            if (string.IsNullOrEmpty(strSource))
                return "Sonstiges";
            return Guid.TryParse(strSource, out _) ? "Sonstige Ausrüstung/Fähigkeit" : strSource;
        }

        private static string FormatSigned(int intValue) => intValue >= 0 ? "+" + intValue : intValue.ToString();

        private IReadOnlyList<CalendarWeek>? _cachedCalendar;
        public IReadOnlyList<CalendarWeek> Calendar
        {
            get
            {
                // Forces the (cheap) settings-file freshness check even on a cache
                // hit below - GetCharacterOptions() invalidates every Read*() cache
                // when the settings file actually changed, but only as a side effect
                // of being called, and _cachedCalendar short-circuits ReadX() (which is where
                // that call would otherwise happen) once already populated.
                GetCharacterOptions();
                return _cachedCalendar ??= ReadCalendar();
            }
        }

        /// <summary>Adds a calendar week in the same save-file representation as the legacy
        /// calendar. Years are unrestricted; weeks use Shadowrun's 1-52 calendar range.</summary>
        public CalendarWeek AddCalendarWeek(int intYear, int intWeek, string strNotes = "")
        {
            if (intWeek is < 1 or > 52)
                throw new ArgumentOutOfRangeException(nameof(intWeek), "A calendar week must be between 1 and 52.");

            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objCalendar = objRoot.SelectSingleNode("calendar") as XmlElement;
            if (objCalendar == null)
            {
                objCalendar = Document.CreateElement("calendar");
                objRoot.AppendChild(objCalendar);
            }

            var objWeek = new CalendarWeek(intYear, intWeek) { Notes = strNotes ?? string.Empty };
            var objNode = Document.CreateElement("week");
            AppendElement(objNode, "guid", objWeek.InternalId);
            AppendElement(objNode, "year", intYear.ToString());
            AppendElement(objNode, "week", intWeek.ToString());
            AppendElement(objNode, "notes", objWeek.Notes);
            objCalendar.AppendChild(objNode);
            Changed?.Invoke();
            return objWeek;
        }

        /// <summary>Changes a saved calendar week's note text.</summary>
        public bool UpdateCalendarWeekNotes(string strInternalId, string strNotes)
        {
            XmlNode? objWeek = FindCalendarWeek(strInternalId);
            if (objWeek == null)
                return false;

            SetChildValue(objWeek, "notes", strNotes ?? string.Empty);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves the complete saved calendar so its first week starts at the selected
        /// year/week, retaining the relative sequence of every following entry.</summary>
        public bool ChangeCalendarStart(int intYear, int intWeek)
        {
            if (intWeek is < 1 or > 52)
                throw new ArgumentOutOfRangeException(nameof(intWeek), "A calendar week must be between 1 and 52.");

            XmlNodeList? objWeeks = Document.SelectNodes("/character/calendar/week");
            if (objWeeks is not { Count: > 0 })
                return false;

            XmlNode objFirst = objWeeks[0]!;
            int intOldYear = int.TryParse(GetValue(objFirst, "year", "0"), out var intParsedYear) ? intParsedYear : intYear;
            int intOldWeek = int.TryParse(GetValue(objFirst, "week", "1"), out var intParsedWeek) ? intParsedWeek : intWeek;
            int intWeekOffset = (intYear - intOldYear) * 52 + intWeek - intOldWeek;

            foreach (XmlNode objWeek in objWeeks)
            {
                int intCurrentYear = int.TryParse(GetValue(objWeek, "year", "0"), out var intParsedCurrentYear) ? intParsedCurrentYear : intOldYear;
                int intCurrentWeek = int.TryParse(GetValue(objWeek, "week", "1"), out var intParsedCurrentWeek) ? intParsedCurrentWeek : intOldWeek;
                int intAbsoluteWeek = intCurrentYear * 52 + (intCurrentWeek - 1) + intWeekOffset;
                int intNewYear = Math.DivRem(intAbsoluteWeek, 52, out int intNewWeekIndex);
                if (intNewWeekIndex < 0) { intNewYear--; intNewWeekIndex += 52; }
                SetChildValue(objWeek, "year", intNewYear.ToString());
                SetChildValue(objWeek, "week", (intNewWeekIndex + 1).ToString());
            }

            Changed?.Invoke();
            return true;
        }

        private static XmlNode? FindBonusChild(string strDataFile, string strContainerTag, string strItemTag,
            string strName, string strChildTag)
        {
            XmlDocument objDoc = XmlManager.Instance.Load(strDataFile);
            XmlNode? objXmlItem = objDoc.SelectSingleNode(
                $"/chummer/{strContainerTag}/{strItemTag}[name = '{strName.Trim()}']");
            return objXmlItem?.SelectSingleNode($"bonus/{strChildTag}");
        }

        private double ApplyRestrictedForbiddenCostMultiplier(double dblCost, string strAvail)
        {
            if (string.IsNullOrEmpty(strAvail))
                return dblCost;

            CharacterOptions objOptions = GetCharacterOptions();
            char chLast = strAvail[strAvail.Length - 1];
            if (chLast == 'R' && objOptions.MultiplyRestrictedCost)
                return dblCost * objOptions.RestrictedCostMultiplier;
            if (chLast == 'F' && objOptions.MultiplyForbiddenCost)
                return dblCost * objOptions.ForbiddenCostMultiplier;
            return dblCost;
        }

        /// <summary>Ports Gear.Create's Unwired convenience children: newly acquired Matrix
        /// Programs, Skillsofts and Autosofts receive no-cost Copy Protection and/or Registration
        /// plugins when the corresponding character settings are enabled. They deliberately bypass
        /// capacity validation and Nuyen deduction, just as the legacy code forces Capacity [0],
        /// Avail 0 and Cost 0. Suites are excluded because their individual programs create their
        /// own children in the legacy importer.</summary>
        private static int ComputeSellRefund(double dblCost, double dblSellPercent) =>
            (int)Math.Round(dblCost * dblSellPercent, MidpointRounding.AwayFromZero);

        public bool PrintLeadershipAlternates => GetCharacterOptions().PrintLeadershipAlternates;
        public bool PrintArcanaAlternates => GetCharacterOptions().PrintArcanaAlternates;
        private void AppendElement(XmlElement objParent, string strName, string strValue)
        {
            var objElement = Document.CreateElement(strName);
            objElement.InnerText = strValue;
            objParent.AppendChild(objElement);
        }

        /// <summary>Resolves <paramref name="nodBonus"/> via <see cref="BonusApplier"/> and persists
        /// each resulting effect as an &lt;improvement&gt; element, in the shape <see
        /// cref="Improvement.Load"/> expects. Ported from clsImprovement.cs's CreateImprovements
        /// (only the non-interactive subset BonusApplier covers) plus CreateImprovement's actual
        /// XML write. Does not fire <see cref="Changed"/> itself - callers already do that for the
        /// item being added.</summary>
        private static XmlNode? FindRuleItemByName(XmlDocument objDocument, string strPath, string strName) =>
            objDocument.SelectNodes(strPath)?.Cast<XmlNode>().FirstOrDefault(objNode =>
                string.Equals(GetValue(objNode, "name", string.Empty), strName, StringComparison.Ordinal));

        public event Action? Changed;

        internal void NotifyChanged() => Changed?.Invoke();

        private void SetRootValue(string strName, string strValue)
        {
            var objRoot = Document.DocumentElement
                ?? throw new InvalidOperationException("Character document has no root element.");
            var objElement = objRoot.SelectSingleNode(strName) as XmlElement;
            if (objElement == null)
            {
                objElement = Document.CreateElement(strName);
                objRoot.AppendChild(objElement);
            }

            objElement.InnerText = strValue ?? string.Empty;
            Changed?.Invoke();
        }

        private void SetChildValue(XmlNode objParent, string strName, string strValue)
        {
            XmlNode? objNode = objParent.SelectSingleNode(strName);
            if (objNode == null)
            {
                objNode = Document.CreateElement(strName);
                objParent.AppendChild(objNode);
            }

            objNode.InnerText = strValue ?? string.Empty;
        }

        private static bool IsProgramCategory(string strCategory) => strCategory is "ARE Programs"
            or "Data Software" or "Malware" or "Matrix Programs" or "Tactical AR Software"
            or "Telematics Infrastructure Software" or "Sensor Software"
            || strCategory.StartsWith("Autosofts", StringComparison.Ordinal);


        private static XmlNode? FindSelectSensewareNode(string strPowerName)
        {
            XmlDocument objPowersDoc = XmlManager.Instance.Load("powers.xml");
            XmlNode? objXmlPower = objPowersDoc.SelectSingleNode($"/chummer/powers/power[name = '{strPowerName.Trim()}']");
            return objXmlPower?.SelectSingleNode("bonus/selectsenseware");
        }

        public bool ConvertSpriteToFreeSprite()
        {
            if (!IsSprite || IsFreeSprite)
                return false;

            XmlDocument objPowersDoc = XmlManager.Instance.Load("critterpowers.xml");
            XmlNode? objDenial = objPowersDoc.SelectSingleNode("/chummer/powers/power[name = 'Denial']");
            if (objDenial == null)
                return false;

            AddCritterPower("Denial", "0", GetValue(objDenial, "source", string.Empty),
                GetValue(objDenial, "page", string.Empty), blnCountTowardsLimit: false);
            SetRootValue("metatypecategory", "Free Sprite");
            return true;
        }

        private static string AspectContainerTag(string strAspectTag) => strAspectTag switch
        {
            "necessity" => "necessities",
            "security" => "securities",
            _ => strAspectTag + "s",
        };

        public string Gender
        {
            get => GetValue("/character/sex", string.Empty);
            set => SetRootValue("sex", value);
        }

        public string EyeColor
        {
            get => GetValue("/character/eyes", string.Empty);
            set => SetRootValue("eyes", value);
        }

        public string HairColor
        {
            get => GetValue("/character/hair", string.Empty);
            set => SetRootValue("hair", value);
        }

        public string Height
        {
            get => GetValue("/character/height", string.Empty);
            set => SetRootValue("height", value);
        }

        public string Weight
        {
            get => GetValue("/character/weight", string.Empty);
            set => SetRootValue("weight", value);
        }

        public string SkinColor
        {
            get => GetValue("/character/skin", string.Empty);
            set => SetRootValue("skin", value);
        }

        /// <summary>Portrait image, base64-encoded - matches the legacy &lt;mugshot&gt; element
        /// (clsCharacter.cs's Save()), which stores the image inline rather than as a file path.</summary>
        public string Mugshot
        {
            get => GetValue("/character/mugshot", string.Empty);
            set => SetRootValue("mugshot", value);
        }

        public string PlayerName
        {
            get => GetValue("/character/playername", string.Empty);
            set => SetRootValue("playername", value);
        }

        public string StreetCred
        {
            get => GetValue("/character/streetcred", "0");
            set => SetRootValue("streetcred", value);
        }

        public string Notoriety
        {
            get => GetValue("/character/notoriety", "0");
            set => SetRootValue("notoriety", value);
        }

        public string PublicAwareness
        {
            get => GetValue("/character/publicawareness", "0");
            set => SetRootValue("publicawareness", value);
        }

        public string Description
        {
            get => GetValue("/character/description", string.Empty);
            set => SetRootValue("description", value);
        }

        public string Background
        {
            get => GetValue("/character/background", string.Empty);
            set => SetRootValue("background", value);
        }

        public string Concept
        {
            get => GetValue("/character/concept", string.Empty);
            set => SetRootValue("concept", value);
        }

        public string Notes
        {
            get => GetValue("/character/notes", string.Empty);
            set => SetRootValue("notes", value);
        }

        private string GetValue(string strXPath, string strFallback)
        {
            var objNode = Document.SelectSingleNode(strXPath);
            string? strValue = objNode?.InnerText;
            return strValue == null || strValue.Length == 0 ? strFallback : strValue;
        }

        private IReadOnlyList<CalendarWeek> ReadCalendar()
        {
            var lstWeeks = new List<CalendarWeek>();
            XmlNodeList? objNodes = Document.SelectNodes("/character/calendar/week");
            if (objNodes == null) return lstWeeks;
            foreach (XmlNode objNode in objNodes)
            {
                var objWeek = new CalendarWeek();
                objWeek.Load(objNode);
                lstWeeks.Add(objWeek);
            }
            return lstWeeks;
        }

        private XmlNode? FindCalendarWeek(string strInternalId)
        {
            if (!Guid.TryParse(strInternalId, out _))
                return null;
            XmlNodeList? objWeeks = Document.SelectNodes("/character/calendar/week");
            if (objWeeks == null)
                return null;
            foreach (XmlNode objWeek in objWeeks)
                if (string.Equals(GetValue(objWeek, "guid", string.Empty), strInternalId, StringComparison.OrdinalIgnoreCase))
                    return objWeek;
            return null;
        }

        private int ApplyCyberlimbAveraging(string strCode, int intMeatValue)
        {
            var objNodes = Document.SelectNodes("/character/cyberwares/cyberware");
            if (objNodes == null) return intMeatValue;

            CharacterOptions objOptions = GetCharacterOptions();
            int intLimbTotal = 0;
            int intLimbCount = 0;
            foreach (XmlNode objNode in objNodes)
            {
                if (GetValue(objNode, "category", string.Empty) != "Cyberlimb")
                    continue;

                bool blnBioware = GetValue(objNode, "improvementsource", string.Empty) == "Bioware";
                string strLimbSlot = GetCyberwareLimbSlot(GetValue(objNode, "name", string.Empty), blnBioware);
                if (string.IsNullOrEmpty(strLimbSlot) || strLimbSlot == objOptions.ExcludeLimbSlot)
                    continue;

                intLimbCount++;
                (int intBody, int intStrength, int intAgility) = ComputeCyberlimbStats(objNode);
                intLimbTotal += strCode switch
                {
                    "BOD" => intBody,
                    "STR" => intStrength,
                    _ => intAgility
                };
            }

            if (intLimbCount == 0)
                return intMeatValue;

            if (intLimbCount < objOptions.LimbCount)
            {
                // Not all of the limbs have been replaced - fill the rest with the meat value to
                // get the average.
                intLimbTotal += intMeatValue * (objOptions.LimbCount - intLimbCount);
                intLimbCount = objOptions.LimbCount;
            }

            return (int)Math.Floor(intLimbTotal / (double)intLimbCount);
        }

        /// <summary>Backing field for <see cref="SetCharacterOptionsForTesting"/> - lets tests
        /// inject a settings profile without needing a real settings/*.xml file on disk.</summary>
        private CharacterOptions? _objCharacterOptionsOverride;

        // Cached per-instance and invalidated by the settings *file's own last-write time* (same
        // pattern as XmlManager.Load's cache) rather than never cached at all: house rules/karma-BP
        // costs can still change from the Options dialog while a character stays open, and this
        // still picks that up (the file's mtime changes when Options.Save() writes it) - but a
        // full character reload (e.g. after any single skill/skill-group rating change) no longer
        // re-reads and re-parses the settings XML from disk once per skill on the character.
        private CharacterOptions? _objCachedCharacterOptions;
        private string? _strCachedCharacterOptionsFileName;
        private DateTime _datCachedCharacterOptionsFileWriteTime;

        internal int CharacterOptionsCacheMissCountForTesting;

        private CharacterOptions GetCharacterOptions()
        {
            if (_objCharacterOptionsOverride != null)
                return _objCharacterOptionsOverride;

            string strSettingsFileName = GetValue("/character/settings", "default.xml");
            string strSettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings", strSettingsFileName);
            DateTime datWriteTime = File.Exists(strSettingsFilePath) ? File.GetLastWriteTimeUtc(strSettingsFilePath) : DateTime.MinValue;

            if (_objCachedCharacterOptions != null && _strCachedCharacterOptionsFileName == strSettingsFileName
                && _datCachedCharacterOptionsFileWriteTime == datWriteTime)
                return _objCachedCharacterOptions;

            CharacterOptionsCacheMissCountForTesting++;
            var objOptions = new CharacterOptions();
            objOptions.Load(strSettingsFileName);
            _objCachedCharacterOptions = objOptions;
            _strCachedCharacterOptionsFileName = strSettingsFileName;
            _datCachedCharacterOptionsFileWriteTime = datWriteTime;
            // Every Read*() cache (Skills, Attributes, ...) bakes in values derived from
            // CharacterOptions (karma costs, house-rule caps, ...) - a real settings-file change
            // invalidates this cache via the mtime check above, but without also invalidating
            // those, they'd keep serving dice pools/costs computed under the OLD options.
            InvalidateReadCaches();
            return objOptions;
        }

        /// <summary>
        /// True if the given rules-data &lt;source&gt; book code is enabled in this character's
        /// game settings (ported from legacy pickers' use of Options.BookEnabled()/BookXPath()).
        /// A blank/missing source is always allowed, matching legacy's behavior of only filtering
        /// items that actually declare a source book.
        /// </summary>
        public bool IsBookEnabled(string strSourceCode) =>
            string.IsNullOrEmpty(strSourceCode) || GetCharacterOptions().BookEnabled(strSourceCode);

        /// <summary>Test-only hook to exercise settings-driven behavior (house rules, karma/BP
        /// costs, ...) without needing a real settings/*.xml file on disk.</summary>
        internal void SetCharacterOptionsForTesting(CharacterOptions objOptions)
        {
            _objCharacterOptionsOverride = objOptions;
            InvalidateReadCaches();
        }

        private IReadOnlyList<CharacterTreeItemData> ReadTreeItems(string strXPath, string strChildXPath)
        {
            var lstItems = new List<CharacterTreeItemData>();
            var objNodes = Document.SelectNodes(strXPath);
            if (objNodes == null) return lstItems;
            foreach (XmlNode objNode in objNodes)
                lstItems.Add(ReadTreeItem(objNode, strChildXPath));
            return lstItems;
        }

        private static (int RcBase, int RcFull) ParseRc(string strRc)
        {
            string strRcBase = "0";
            string strRcFull = "0";
            if (strRc.Contains('('))
            {
                if (strRc.StartsWith("(", StringComparison.Ordinal))
                {
                    strRcFull = strRc;
                }
                else
                {
                    int intPos = strRc.IndexOf('(');
                    strRcBase = strRc.Substring(0, intPos);
                    strRcFull = strRc.Substring(intPos);
                }
            }
            else
            {
                strRcBase = strRc;
                strRcFull = strRc;
            }

            int intRcBase = int.TryParse(strRcBase, out var b) ? b : 0;
            int intRcFull = int.TryParse(strRcFull.Replace("(", string.Empty).Replace(")", string.Empty), out var f) ? f : 0;
            if (intRcBase < 0)
                intRcBase = 0;
            return (intRcBase, intRcFull);
        }

        // Ported from clsEquipment.cs's Weapon.DicePool: which Active Skill a weapon Category
        // rolls against.
        private static CharacterTreeItemData ReadTreeItem(XmlNode objNode, params string[] lstChildXPaths)
        {
            var objItem = new CharacterTreeItemData(GetValue(objNode, "name", string.Empty),
                GetValue(objNode, "category", string.Empty), GetValue(objNode, "rating", "0"),
                GetValue(objNode, "equipped", "False") == "True",
                GetValue(objNode, "cost", string.Empty), GetValue(objNode, "avail", string.Empty),
                GetValue(objNode, "qty", "1"));
            objItem.SetItemGuid(GetValue(objNode, "guid", string.Empty));
            objItem.SetNotes(GetValue(objNode, "notes", string.Empty));
            objItem.SetCustomName(GetValue(objNode, "gearname", string.Empty));
            foreach (string strChildXPath in lstChildXPaths)
            {
                if (string.IsNullOrEmpty(strChildXPath)) continue;
                var objChildren = objNode.SelectNodes(strChildXPath);
                if (objChildren == null) continue;
                foreach (XmlNode objChild in objChildren)
                    objItem.Children.Add(ReadTreeItem(objChild,
                        strChildXPath == "gears/gear" ? "children/gear" : strChildXPath));
            }
            return objItem;
        }

        private static string GetValue(XmlNode objNode, string strName, string strFallback)
        {
            var objChild = objNode.SelectSingleNode(strName);
            string? strValue = objChild?.InnerText;
            return strValue == null || strValue.Length == 0 ? strFallback : strValue;
        }

    }

}
