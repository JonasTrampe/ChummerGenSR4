using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// List of Grades for either Cyberware or Bioware.
	/// </summary>
	public class GradeList : IEnumerable<Grade>
	{
		private List<Grade> _lstGrades = new List<Grade>();

		#region Methods
		/// <summary>
		/// Fill the list of CyberwareGrades from the XML files.
		/// </summary>
		/// <param name="objXmlDocument">Document containing the grade definitions.</param>
		public void LoadList(XmlDocument objXmlDocument)
		{
			foreach (XmlNode objNode in objXmlDocument.SelectNodes("/chummer/grades/grade"))
			{
				Grade objGrade = new Grade();
				objGrade.Load(objNode);
				_lstGrades.Add(objGrade);
			}
		}

		/// <summary>
		/// Retrieve the Standard Grade from the list.
		/// </summary>
		public Grade GetGrade(string strGrade)
		{
			Grade objReturn = new Grade();
			foreach (Grade objGrade in _lstGrades)
			{
				if (objGrade.Name == "Standard")
				{
					objReturn = objGrade;
					break;
				}
			}

			if (strGrade != "Standard")
			{
				foreach (Grade objGrade in _lstGrades)
				{
					if (objGrade.Name == strGrade)
					{
						objReturn = objGrade;
						break;
					}
				}
			}

			return objReturn;
		}
		#endregion

		#region Enumeration Methods
		public IEnumerator<Grade> GetEnumerator()
		{
			return this._lstGrades.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		#endregion
	}

}

