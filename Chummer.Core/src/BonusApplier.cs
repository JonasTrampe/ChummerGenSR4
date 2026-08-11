using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>One resolved effect a <c>&lt;bonus&gt;</c> node should produce, ready to be
    /// persisted as a <see cref="Improvement"/>-shaped &lt;improvement&gt; element. Kept separate
    /// from the actual XML write so the parsing side (this file) stays pure and independently
    /// testable - <c>CharacterFileService</c> owns turning these into saved character state.</summary>
    public sealed record ImprovementSpec(
        ImprovementType Type,
        string ImprovedName = "",
        int Value = 0,
        int Rating = 1,
        int Minimum = 0,
        int Maximum = 0,
        int Augmented = 0,
        int AugmentedMaximum = 0,
        string UniqueName = "",
        bool AddToRating = false);

    /// <summary>Ported (a deliberately scoped subset) from clsImprovement.cs's CreateImprovements:
    /// given a rules-data &lt;bonus&gt; node, resolves it into the list of effects it should apply.
    /// Only covers the non-interactive bonus node types (no &lt;selecttext&gt;/&lt;selectskill&gt;/
    /// &lt;selectattribute&gt; - those need a player-facing picker, which is a separate, not yet
    /// built "Selectable Improvement" flow - see FEATURE_CHECKLIST.md). Everything handled here
    /// covers the large majority of real usage in this port's own data files (specificattribute,
    /// specificskill, conditionmonitor, skillcategory, skillgroup, skillattribute, initiative/
    /// initiativepass, notoriety, armor, reach, unarmed dv/ap, lifestylecost).</summary>
    public static class BonusApplier
    {
        public static IReadOnlyList<ImprovementSpec> Parse(XmlNode? nodBonus, string strRating, string strUnique)
        {
            var lstResult = new List<ImprovementSpec>();
            if (nodBonus == null)
                return lstResult;

            ParseSpecificAttribute(nodBonus, strRating, strUnique, lstResult);
            ParseSpecificSkill(nodBonus, strRating, strUnique, lstResult);
            ParseSkillCategory(nodBonus, strRating, strUnique, lstResult);
            ParseSkillGroup(nodBonus, strRating, strUnique, lstResult);
            ParseSkillAttribute(nodBonus, strRating, strUnique, lstResult);
            ParseConditionMonitor(nodBonus, strRating, strUnique, lstResult);
            ParseArmor(nodBonus, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "reach", ImprovementType.Reach, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "unarmeddv", ImprovementType.UnarmedDv, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "unarmedap", ImprovementType.UnarmedAp, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "initiative", ImprovementType.Initiative, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "initiativepass", ImprovementType.InitiativePass, strRating, "initiativepass", lstResult);
            ParseSimpleValue(nodBonus, "lifestylecost", ImprovementType.LifestyleCost, strRating, strUnique, lstResult);
            ParseSimpleValue(nodBonus, "notoriety", ImprovementType.Notoriety, strRating, "", lstResult);

            return lstResult;
        }

        // Ported from clsImprovement.cs's "specificattribute" handler. The ESS special case
        // (a plain Essence-maximum delta rather than a named-attribute bonus) is preserved.
        private static void ParseSpecificAttribute(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult)
        {
            foreach (XmlNode objNode in nodBonus.SelectNodes("specificattribute") ?? EmptyNodeList())
            {
                string strName = ChildText(objNode, "name");
                if (string.IsNullOrEmpty(strName))
                    continue;

                if (strName == "ESS")
                {
                    lstResult.Add(new ImprovementSpec(ImprovementType.Essence,
                        Value: (int)RatingExpression.Evaluate(ChildText(objNode, "val"), strRating)));
                    continue;
                }

                string strAttribute = objNode["affectbase"] != null ? strName + "Base" : strName;
                string strUseUnique = AttributePrecedence(objNode, "name", strUnique);
                lstResult.Add(new ImprovementSpec(ImprovementType.Attribute, strAttribute,
                    Minimum: (int)RatingExpression.Evaluate(ChildText(objNode, "min"), strRating),
                    Maximum: (int)RatingExpression.Evaluate(ChildText(objNode, "max"), strRating),
                    Augmented: (int)RatingExpression.Evaluate(ChildText(objNode, "val"), strRating),
                    AugmentedMaximum: (int)RatingExpression.Evaluate(ChildText(objNode, "aug"), strRating),
                    UniqueName: strUseUnique));
            }
        }

        // Ported from clsImprovement.cs's "specificskill" handler.
        private static void ParseSpecificSkill(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult)
        {
            foreach (XmlNode objNode in nodBonus.SelectNodes("specificskill") ?? EmptyNodeList())
            {
                string strName = ChildText(objNode, "name");
                if (string.IsNullOrEmpty(strName))
                    continue;

                bool blnAddToRating = ChildText(objNode, "applytorating") == "yes";
                if (objNode["bonus"] != null)
                    lstResult.Add(new ImprovementSpec(ImprovementType.Skill, strName,
                        Value: (int)RatingExpression.Evaluate(ChildText(objNode, "bonus"), strRating),
                        UniqueName: strUnique, AddToRating: blnAddToRating));
                if (objNode["max"] != null)
                    lstResult.Add(new ImprovementSpec(ImprovementType.Skill, strName,
                        Maximum: (int)RatingExpression.Evaluate(ChildText(objNode, "max"), strRating),
                        UniqueName: strUnique, AddToRating: blnAddToRating));
            }
        }

        private static void ParseSkillCategory(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult) =>
            ParseSkillGrouping(nodBonus, "skillcategory", ImprovementType.SkillCategory, strRating, strUnique, lstResult);

        private static void ParseSkillGroup(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult) =>
            ParseSkillGrouping(nodBonus, "skillgroup", ImprovementType.SkillGroup, strRating, strUnique, lstResult);

        private static void ParseSkillAttribute(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult) =>
            ParseSkillGrouping(nodBonus, "skillattribute", ImprovementType.SkillAttribute, strRating, strUnique, lstResult);

        // Shared shape of clsImprovement.cs's skillcategory/skillgroup/skillattribute handlers:
        // name + bonus, optional applytorating, optional exclude (not modeled as a separate field
        // here - excludes are rare enough in this port's data that they're out of MVP scope; see
        // FEATURE_CHECKLIST.md).
        private static void ParseSkillGrouping(XmlNode nodBonus, string strTag, ImprovementType eType,
            string strRating, string strUnique, List<ImprovementSpec> lstResult)
        {
            foreach (XmlNode objNode in nodBonus.SelectNodes(strTag) ?? EmptyNodeList())
            {
                string strName = ChildText(objNode, "name");
                if (string.IsNullOrEmpty(strName) || objNode["bonus"] == null)
                    continue;

                bool blnAddToRating = ChildText(objNode, "applytorating") == "yes";
                lstResult.Add(new ImprovementSpec(eType, strName,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "bonus"), strRating),
                    UniqueName: strUnique, AddToRating: blnAddToRating));
            }
        }

        // Ported from clsImprovement.cs's "conditionmonitor" handler (physical/stun/threshold/
        // thresholdoffset/overflow sub-elements).
        private static void ParseConditionMonitor(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult)
        {
            XmlNode? objNode = nodBonus.SelectSingleNode("conditionmonitor");
            if (objNode == null)
                return;

            if (objNode["physical"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.PhysicalCm,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "physical"), strRating), UniqueName: strUnique));
            if (objNode["stun"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.StunCm,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "stun"), strRating), UniqueName: strUnique));
            if (objNode["threshold"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.CmThreshold,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "threshold"), strRating),
                    UniqueName: AttributePrecedence(objNode["threshold"]!, null, strUnique)));
            if (objNode["thresholdoffset"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.CmThresholdOffset,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "thresholdoffset"), strRating),
                    UniqueName: AttributePrecedence(objNode["thresholdoffset"]!, null, strUnique)));
            if (objNode["overflow"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.CmOverflow,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "overflow"), strRating), UniqueName: strUnique));
        }

        // Ported from clsImprovement.cs's "armor" handler (b = ballistic, i = impact).
        private static void ParseArmor(XmlNode nodBonus, string strRating, string strUnique,
            List<ImprovementSpec> lstResult)
        {
            XmlNode? objNode = nodBonus.SelectSingleNode("armor");
            if (objNode == null)
                return;

            if (objNode["b"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.BallisticArmor,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "b"), strRating), UniqueName: strUnique));
            if (objNode["i"] != null)
                lstResult.Add(new ImprovementSpec(ImprovementType.ImpactArmor,
                    Value: (int)RatingExpression.Evaluate(ChildText(objNode, "i"), strRating), UniqueName: strUnique));
        }

        // Shared shape of the many single-value bonus tags (reach/unarmeddv/unarmedap/initiative/
        // initiativepass/lifestylecost/notoriety/...): <tag>value-or-expression</tag>.
        private static void ParseSimpleValue(XmlNode nodBonus, string strTag, ImprovementType eType,
            string strRating, string strUnique, List<ImprovementSpec> lstResult)
        {
            XmlNode? objNode = nodBonus.SelectSingleNode(strTag);
            if (objNode == null)
                return;

            lstResult.Add(new ImprovementSpec(eType,
                Value: (int)RatingExpression.Evaluate(objNode.InnerText, strRating), UniqueName: strUnique));
        }

        private static string ChildText(XmlNode objNode, string strName) => objNode[strName]?.InnerText ?? string.Empty;

        private static string AttributePrecedence(XmlNode objNode, string? strChildName, string strUnique)
        {
            XmlNode? objTarget = strChildName == null ? objNode : objNode[strChildName];
            string? strPrecedence = objTarget?.Attributes?["precedence"]?.InnerText;
            return strPrecedence == null ? strUnique : "precedence" + strPrecedence;
        }

        private static XmlNodeList EmptyNodeList()
        {
            var objDoc = new XmlDocument();
            return objDoc.CreateElement("empty").ChildNodes;
        }
    }
}
