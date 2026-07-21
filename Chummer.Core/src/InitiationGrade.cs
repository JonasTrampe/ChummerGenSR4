using System;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     An Initiation Grade.
    /// </summary>
    public class InitiationGrade
    {
        private readonly double _dblKarmaInitiation;
        private Guid _guiId;

        #region Constructor, Create, Save, and Load Methods

        public InitiationGrade(double dblKarmaInitiation)
        {
            // Create the GUID for the new InitiationGrade.
            _guiId = Guid.NewGuid();
            _dblKarmaInitiation = dblKarmaInitiation;
        }

        /// Create an Intiation Grade from an XmlNode and return the TreeNodes for it.
        /// <param name="intGrade">Grade number.</param>
        /// <param name="blnTechnomancer">Whether or not the character is a Technomancer.</param>
        /// <param name="blnGroup">Whether or not a Group was used.</param>
        /// <param name="blnOrdeal">Whether or not an Ordeal was used.</param>
        public void Create(int intGrade, bool blnTechnomancer, bool blnGroup, bool blnOrdeal)
        {
            Grade = intGrade;
            Technomancer = blnTechnomancer;
            Group = blnGroup;
            Ordeal = blnOrdeal;
        }

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("initiationgrade");
            objWriter.WriteElementString("guid", _guiId.ToString());
            objWriter.WriteElementString("res", Technomancer.ToString());
            objWriter.WriteElementString("grade", Grade.ToString());
            objWriter.WriteElementString("group", Group.ToString());
            objWriter.WriteElementString("ordeal", Ordeal.ToString());
            objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the Initiation Grade from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            _guiId = Guid.Parse(objNode["guid"].InnerText);
            Technomancer = Convert.ToBoolean(objNode["res"].InnerText);
            Grade = Convert.ToInt32(objNode["grade"].InnerText);
            Group = Convert.ToBoolean(objNode["group"].InnerText);
            Ordeal = Convert.ToBoolean(objNode["ordeal"].InnerText);
            try
            {
                Notes = objNode["notes"].InnerText;
            }
            catch
            {
            }
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Internal identifier which will be used to identify this Initiation Grade in the Improvement system.
        /// </summary>
        public string InternalId => _guiId.ToString();

        /// <summary>
        ///     Initiate Grade.
        /// </summary>
        public int Grade { get; set; }

        /// <summary>
        ///     Whether or not a Group was used.
        /// </summary>
        public bool Group { get; set; }

        /// <summary>
        ///     Whether or not an Ordeal was used.
        /// </summary>
        public bool Ordeal { get; set; }

        /// <summary>
        ///     Whether or not the Initiation Grade is for a Technomancer.
        /// </summary>
        public bool Technomancer { get; set; }

        #endregion

        #region Complex Properties

        /// <summary>
        ///     The Initiation Grade's Karma cost.
        /// </summary>
        public int KarmaCost
        {
            get
            {
                var intCost = 0;
                var dblCost = 10.0 + Grade * _dblKarmaInitiation;
                var dblMultiplier = 1.0;

                // Discount for Group.
                if (Group)
                    dblMultiplier -= 0.2;

                // Discount for Ordeal.
                if (Ordeal)
                    dblMultiplier -= 0.2;

                intCost = Convert.ToInt32(Math.Ceiling(dblCost * dblMultiplier));

                return intCost;
            }
        }


        /// <summary>
        ///     Notes.
        /// </summary>
        public string Notes { get; set; } = "";

        #endregion
    }
}