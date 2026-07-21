using System;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     Undo information for an Expense Log Entry.
    /// </summary>
    public class ExpenseUndo
    {
        #region Helper Methods

        /// <summary>
        ///     Convert a string to a KarmaExpenseType.
        /// </summary>
        /// <param name="strValue">String value to convert.</param>
        public KarmaExpenseType ConvertToKarmaExpenseType(string strValue)
        {
            switch (strValue)
            {
                case "AddComplexForm":
                    return KarmaExpenseType.AddComplexForm;
                case "AddComplexFormOption":
                    return KarmaExpenseType.AddComplexFormOption;
                case "AddMartialArt":
                    return KarmaExpenseType.AddMartialArt;
                case "AddMartialArtManeuver":
                    return KarmaExpenseType.AddMartialArtManeuver;
                case "AddMetamagic":
                    return KarmaExpenseType.AddMetamagic;
                case "AddQuality":
                    return KarmaExpenseType.AddQuality;
                case "AddSkill":
                    return KarmaExpenseType.AddSkill;
                case "AddSpell":
                    return KarmaExpenseType.AddSpell;
                case "BindFocus":
                    return KarmaExpenseType.BindFocus;
                case "ImproveAttribute":
                    return KarmaExpenseType.ImproveAttribute;
                case "ImproveComplexForm":
                    return KarmaExpenseType.ImproveComplexForm;
                case "ImproveComplexFormOption":
                    return KarmaExpenseType.ImproveComplexFormOption;
                case "ImproveInitiateGrade":
                    return KarmaExpenseType.ImproveInitiateGrade;
                case "ImproveMartialArt":
                    return KarmaExpenseType.ImproveMartialArt;
                case "ImproveSkill":
                    return KarmaExpenseType.ImproveSkill;
                case "ImproveSkillGroup":
                    return KarmaExpenseType.ImproveSkillGroup;
                case "ManualAdd":
                    return KarmaExpenseType.ManualAdd;
                case "ManualSubtract":
                    return KarmaExpenseType.ManualSubtract;
                case "RemoveQuality":
                    return KarmaExpenseType.RemoveQuality;
                case "SkillSpec":
                    return KarmaExpenseType.SkillSpec;
                case "JoinGroup":
                    return KarmaExpenseType.JoinGroup;
                case "LeaveGroup":
                    return KarmaExpenseType.LeaveGroup;
                default:
                    return KarmaExpenseType.ManualAdd;
            }
        }

        /// <summary>
        ///     Convert a string to a NuyenExpenseType.
        /// </summary>
        /// <param name="strValue">String value to convert.</param>
        public NuyenExpenseType ConvertToNuyenExpenseType(string strValue)
        {
            switch (strValue)
            {
                case "AddArmor":
                    return NuyenExpenseType.AddArmor;
                case "AddArmorGear":
                    return NuyenExpenseType.AddArmorGear;
                case "AddArmorMod":
                    return NuyenExpenseType.AddArmorMod;
                case "AddCyberware":
                    return NuyenExpenseType.AddCyberware;
                case "AddGear":
                    return NuyenExpenseType.AddGear;
                case "AddVehicle":
                    return NuyenExpenseType.AddVehicle;
                case "AddVehicleGear":
                    return NuyenExpenseType.AddVehicleGear;
                case "AddVehicleMod":
                    return NuyenExpenseType.AddVehicleMod;
                case "AddVehicleWeapon":
                    return NuyenExpenseType.AddVehicleWeapon;
                case "AddVehicleWeaponAccessory":
                    return NuyenExpenseType.AddVehicleWeaponAccessory;
                case "AddVehicleWeaponMod":
                    return NuyenExpenseType.AddVehicleWeaponMod;
                case "AddWeapon":
                    return NuyenExpenseType.AddWeapon;
                case "AddWeaponAccessory":
                    return NuyenExpenseType.AddWeaponAccessory;
                case "AddWeaponMod":
                    return NuyenExpenseType.AddWeaponMod;
                case "IncreaseLifestyle":
                    return NuyenExpenseType.IncreaseLifestyle;
                case "ManualAdd":
                    return NuyenExpenseType.ManualAdd;
                case "ManualSubtract":
                    return NuyenExpenseType.ManualSubtract;
                case "AddVehicleModCyberware":
                    return NuyenExpenseType.AddVehicleModCyberware;
                case "AddCyberwareGear":
                    return NuyenExpenseType.AddCyberwareGear;
                case "AddWeaponGear":
                    return NuyenExpenseType.AddWeaponGear;
                case "CredstickDeposit":
                    return NuyenExpenseType.CredstickDeposit;
                case "CredstickWithdrawal":
                    return NuyenExpenseType.CredstickWithdrawal;
                default:
                    return NuyenExpenseType.ManualAdd;
            }
        }

        #endregion

        #region Constructor, Create, Save, and Load Methods

        /// <summary>
        ///     Create the ExpenseUndo Entry.
        /// </summary>
        /// <param name="objExpenseType">Karma expense type.</param>
        /// <param name="strObjectId">Object identifier.</param>
        public void CreateKarma(KarmaExpenseType objExpenseType, string strObjectId)
        {
            KarmaType = objExpenseType;
            ObjectId = strObjectId;
        }

        /// <summary>
        ///     Create the ExpenseUndo Entry.
        /// </summary>
        /// <param name="objExpenseType">Nuyen expense type.</param>
        /// <param name="strObjectId">Object identifier.</param>
        /// <param name="intQty">Amount of Nuyen.</param>
        public void CreateNuyen(NuyenExpenseType objExpenseType, string strObjectId, int intQty = 0)
        {
            NuyenType = objExpenseType;
            ObjectId = strObjectId;
            Qty = intQty;
        }

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("undo");
            objWriter.WriteElementString("karmatype", KarmaType.ToString());
            objWriter.WriteElementString("nuyentype", NuyenType.ToString());
            objWriter.WriteElementString("objectid", ObjectId);
            objWriter.WriteElementString("qty", Qty.ToString());
            objWriter.WriteElementString("extra", Extra);
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the KarmaLogEntry from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            KarmaType = ConvertToKarmaExpenseType(GetRequiredValue(objNode, "karmatype"));
            NuyenType = ConvertToNuyenExpenseType(GetRequiredValue(objNode, "nuyentype"));
            ObjectId = GetRequiredValue(objNode, "objectid");
            Qty = Convert.ToInt32(GetRequiredValue(objNode, "qty"));
            Extra = GetRequiredValue(objNode, "extra");
        }

        private static string GetRequiredValue(XmlNode objNode, string strName)
        {
            return objNode[strName]?.InnerText
                   ?? throw new InvalidDataException("Expense undo entry is missing the required '" + strName + "' value.");
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Karma Expense Type.
        /// </summary>
        public KarmaExpenseType KarmaType { get; set; }

        /// <summary>
        ///     Nuyen Expense Type.
        /// </summary>
        public NuyenExpenseType NuyenType { get; set; }

        /// <summary>
        ///     Object InternalId.
        /// </summary>
        public string ObjectId { get; set; } = "";

        /// <summary>
        ///     Quantity of items added (Nuyen only).
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        ///     Extra information.
        /// </summary>
        public string Extra { get; set; } = "";

        #endregion
    }
}
