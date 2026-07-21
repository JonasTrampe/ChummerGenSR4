using System;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    public class CalendarWeek
    {
        private Guid _guiId;

        #region Constructor, Save, Load, and Print Methods

        public CalendarWeek()
        {
            // Create the GUID for the new CalendarWeek.
            _guiId = Guid.NewGuid();
        }

        public CalendarWeek(int intYear, int intWeek)
        {
            // Create the GUID for the new CalendarWeek.
            _guiId = Guid.NewGuid();
            Year = intYear;
            Week = intWeek;
        }

        /// <summary>
        ///     Save the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Save(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("week");
            objWriter.WriteElementString("guid", _guiId.ToString());
            objWriter.WriteElementString("year", Year.ToString());
            objWriter.WriteElementString("week", Week.ToString());
            objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        /// <summary>
        ///     Load the Calendar Week from the XmlNode.
        /// </summary>
        /// <param name="objNode">XmlNode to load.</param>
        public void Load(XmlNode objNode)
        {
			_guiId = Guid.Parse(RequiredValue(objNode, "guid"));
			Year = Convert.ToInt32(RequiredValue(objNode, "year"));
			Week = Convert.ToInt32(RequiredValue(objNode, "week"));
			Notes = objNode["notes"]?.InnerText ?? string.Empty;
		}

		private static string RequiredValue(XmlNode objNode, string strName)
			=> objNode[strName]?.InnerText ?? throw new InvalidDataException("Calendar week is missing '" + strName + "'.");

        /// <summary>
        ///     Print the object's XML to the XmlWriter.
        /// </summary>
        /// <param name="objWriter">XmlTextWriter to write with.</param>
        public void Print(XmlTextWriter objWriter)
        {
            objWriter.WriteStartElement("week");
            objWriter.WriteElementString("year", Year.ToString());
            objWriter.WriteElementString("month", Month.ToString());
            objWriter.WriteElementString("week", MonthWeek.ToString());
            objWriter.WriteElementString("notes", Notes);
            objWriter.WriteEndElement();
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Internal identifier which will be used to identify this Calendar Week in the Improvement system.
        /// </summary>
        public string InternalId => _guiId.ToString();

        /// <summary>
        ///     Year.
        /// </summary>
        public int Year { get; set; } = 2072;

        /// <summary>
        ///     Month.
        /// </summary>
        public int Month
        {
            get
            {
                switch (Week)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        return 1;
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                        return 2;
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                    case 13:
                        return 3;
                    case 14:
                    case 15:
                    case 16:
                    case 17:
                        return 4;
                    case 18:
                    case 19:
                    case 20:
                    case 21:
                        return 5;
                    case 22:
                    case 23:
                    case 24:
                    case 25:
                    case 26:
                        return 6;
                    case 27:
                    case 28:
                    case 29:
                    case 30:
                        return 7;
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                        return 8;
                    case 35:
                    case 36:
                    case 37:
                    case 38:
                    case 39:
                        return 9;
                    case 40:
                    case 41:
                    case 42:
                    case 43:
                        return 10;
                    case 44:
                    case 45:
                    case 46:
                    case 47:
                        return 11;
                    default:
                        return 12;
                }
            }
        }

        /// <summary>
        ///     Week of the month.
        /// </summary>
        public int MonthWeek
        {
            get
            {
                switch (Week)
                {
                    case 1:
                    case 5:
                    case 9:
                    case 14:
                    case 18:
                    case 22:
                    case 27:
                    case 31:
                    case 35:
                    case 40:
                    case 44:
                    case 48:
                        return 1;
                    case 2:
                    case 6:
                    case 10:
                    case 15:
                    case 19:
                    case 23:
                    case 28:
                    case 32:
                    case 36:
                    case 41:
                    case 45:
                    case 49:
                        return 2;
                    case 3:
                    case 7:
                    case 11:
                    case 16:
                    case 20:
                    case 24:
                    case 29:
                    case 33:
                    case 37:
                    case 42:
                    case 46:
                    case 50:
                        return 3;
                    case 4:
                    case 8:
                    case 12:
                    case 17:
                    case 21:
                    case 25:
                    case 30:
                    case 34:
                    case 38:
                    case 43:
                    case 47:
                    case 51:
                        return 4;
                    default:
                        return 5;
                }
            }
        }


        /// <summary>
        ///     Week.
        /// </summary>
        public int Week { get; set; } = 1;

        /// <summary>
        ///     Notes.
        /// </summary>
        public string Notes { get; set; } = "";

        #endregion
    }
}
