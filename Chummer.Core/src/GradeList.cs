using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Core
{
    /// <summary>
    ///     List of Grades for either Cyberware or Bioware.
    /// </summary>
    public class GradeList : IEnumerable<Grade>
    {
        private readonly List<Grade> _lstGrades = new();

        #region Methods

        /// <summary>
        ///     Fill the list of CyberwareGrades from the XML files.
        /// </summary>
        /// <param name="objXmlDocument">Document containing the grade definitions.</param>
        public void LoadList(XmlDocument objXmlDocument)
        {
			var objNodes = objXmlDocument.SelectNodes("/chummer/grades/grade");
			if (objNodes == null)
				return;
			foreach (XmlNode objNode in objNodes)
            {
                var objGrade = new Grade();
                objGrade.Load(objNode);
                _lstGrades.Add(objGrade);
            }
        }

        /// <summary>
        ///     Retrieve the Standard Grade from the list.
        /// </summary>
        public Grade GetGrade(string strGrade)
        {
            var objReturn = new Grade();
            foreach (var objGrade in _lstGrades)
                if (objGrade.Name == "Standard")
                {
                    objReturn = objGrade;
                    break;
                }

            if (strGrade != "Standard")
                foreach (var objGrade in _lstGrades)
                    if (objGrade.Name == strGrade)
                    {
                        objReturn = objGrade;
                        break;
                    }

            return objReturn;
        }

        #endregion

        #region Enumeration Methods

        public IEnumerator<Grade> GetEnumerator()
        {
            return _lstGrades.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
