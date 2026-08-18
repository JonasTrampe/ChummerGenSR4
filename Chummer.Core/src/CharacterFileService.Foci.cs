using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Core
{
    public sealed partial class CharacterDocument
    {
        public bool UnstackFocus(Guid guiStackId)
        {
            XmlNode? objStack = Document.SelectNodes("/character/stackedfoci/stackedfocus")?.Cast<XmlNode>()
                .FirstOrDefault(node => string.Equals(GetValue(node, "guid", string.Empty), guiStackId.ToString(),
                    StringComparison.OrdinalIgnoreCase));
            if (objStack == null || GetValue(objStack, "bonded", "False") == "True"
                || !Guid.TryParse(GetValue(objStack, "gearid", string.Empty), out Guid guiCompositeId))
                return false;
            XmlNode? objComposite = FindGearNodeByGuid(guiCompositeId);
            XmlNode? objComponents = objStack.SelectSingleNode("gears");
            XmlElement? objRootGears = Document.SelectSingleNode("/character/gears") as XmlElement;
            if (objComposite?.ParentNode == null || objComponents == null || objRootGears == null)
                return false;

            foreach (XmlNode objGear in objComponents.SelectNodes("gear")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
                objRootGears.AppendChild(objGear.CloneNode(deep: true));
            objComposite.ParentNode.RemoveChild(objComposite);
            objStack.ParentNode?.RemoveChild(objStack);
            Changed?.Invoke();
            return true;
        }

        public bool UnbindStackedFocus(Guid guiStackId)
        {
            XmlNode? objStack = FindStackedFocusNode(guiStackId);
            if (objStack == null || GetValue(objStack, "bonded", "False") != "True")
                return false;
            SetChildValue(objStack, "bonded", "False");
            RemoveBonusImprovements(ImprovementSource.StackedFocus, guiStackId.ToString());
            Changed?.Invoke();
            return true;
        }

        private XmlNode? FindStackedFocusNode(Guid guiStackId) => Document.SelectNodes("/character/stackedfoci/stackedfocus")?
            .Cast<XmlNode>().FirstOrDefault(node => string.Equals(GetValue(node, "guid", string.Empty),
                guiStackId.ToString(), StringComparison.OrdinalIgnoreCase));

        private void ApplyStackedFocusBonuses(XmlNode objStack, Guid guiStackId)
        {
            foreach (XmlNode objComponent in objStack.SelectNodes("gears/gear")?.Cast<XmlNode>() ?? Enumerable.Empty<XmlNode>())
            {
                string strName = GetValue(objComponent, "name", string.Empty);
                string strCategory = GetValue(objComponent, "category", string.Empty);
                XmlNode? objRulesGear = XmlManager.Instance.Load("gear.xml").SelectNodes("/chummer/gears/gear")?
                    .Cast<XmlNode>().FirstOrDefault(node => GetValue(node, "name", string.Empty) == strName
                        && GetValue(node, "category", string.Empty) == strCategory);
                ApplyBonus(objRulesGear?.SelectSingleNode("bonus"), ImprovementSource.StackedFocus, guiStackId.ToString(),
                    GetValue(objComponent, "rating", "0"));
            }
        }

        private int GetFocusKarmaMultiplier(string strFocusName)
        {
            int intParenthesis = strFocusName.IndexOf('(');
            if (intParenthesis >= 0)
                strFocusName = strFocusName.Substring(0, intParenthesis).TrimEnd();

            CharacterOptions objOptions = GetCharacterOptions();
            return strFocusName switch
            {
                "Symbolic Link Focus" => objOptions.KarmaSymbolicLinkFocus,
                "Sustaining Focus" => objOptions.KarmaSustainingFocus,
                "Counterspelling Focus" => objOptions.KarmaCounterspellingFocus,
                "Banishing Focus" => objOptions.KarmaBanishingFocus,
                "Binding Focus" => objOptions.KarmaBindingFocus,
                "Weapon Focus" => objOptions.KarmaWeaponFocus,
                "Spellcasting Focus" => objOptions.KarmaSpellcastingFocus,
                "Summoning Focus" => objOptions.KarmaSummoningFocus,
                "Anchoring Focus" => objOptions.KarmaAnchoringFocus,
                "Centering Focus" => objOptions.KarmaCenteringFocus,
                "Masking Focus" => objOptions.KarmaMaskingFocus,
                "Shielding Focus" => objOptions.KarmaShieldingFocus,
                "Power Focus" => objOptions.KarmaPowerFocus,
                "Divining Focus" => objOptions.KarmaDiviningFocus,
                "Dowsing Focus" => objOptions.KarmaDowsingFocus,
                "Infusion Focus" => objOptions.KarmaInfusionFocus,
                _ => 1,
            };
        }

        /// <summary>Ported from frmCareer.cs's cmdAddSpirit_Click, simplified to the fields the
        /// port's Spirits list actually displays - Spirits/Sprites are freely typed (no rules-data
        /// cross-reference like Gear/Cyberware), so this doesn't need a picker dialog.</summary>
    }
}
