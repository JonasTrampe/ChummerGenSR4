using System;
using System.Globalization;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     Grade of Cyberware or Bioware.
    /// </summary>
    public class Grade
    {
        private string _strAltName = "";

        #region Constructor and Load Methods

        /// <summary>
        ///     Load the Grade from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
            Name = objNode["name"]?.InnerText ?? "";
            _strAltName = objNode["translate"]?.InnerText ?? "";
            Essence = Convert.ToDecimal(objNode["ess"]?.InnerText, CultureInfo.GetCultureInfo("en-US"));
            Cost = Convert.ToDouble(objNode["cost"]?.InnerText, CultureInfo.GetCultureInfo("en-US"));
            Avail = Convert.ToInt32(objNode["avail"]?.InnerText, CultureInfo.GetCultureInfo("en-US"));
            Source = objNode["source"]?.InnerText ?? "";
        }

        #endregion

        #region Properties

        /// <summary>
        ///     The English name of the Grade.
        /// </summary>
        public string Name { get; private set; } = "Standard";

        /// <summary>
        ///     The name of the Grade as it should be displayed in lists.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (_strAltName != string.Empty)
                    return _strAltName;
                return Name;
            }
        }

        /// <summary>
        ///     The Grade's Essence cost multiplier.
        /// </summary>
        public decimal Essence { get; private set; } = 1.0m;

        /// <summary>
        ///     The Grade's cost multiplier.
        /// </summary>
        public double Cost { get; private set; } = 1.0;

        /// <summary>
        ///     The Grade's Availability modifier.
        /// </summary>
        public int Avail { get; private set; }

        /// <summary>
        ///     Sourcebook.
        /// </summary>
        public string Source { get; private set; } = "SR4";

        /// <summary>
        ///     Whether or not the Grade is for Adapsin.
        /// </summary>
        public bool Adapsin => Name.Contains("(Adapsin)");

        /// <summary>
        ///     Whether or not this is a Second-Hand Grade.
        /// </summary>
        public bool SecondHand => Name.Contains("(Second-Hand)");

        #endregion
    }
}