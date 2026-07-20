using System;
using System.Xml;

namespace Chummer
{
	public class MentorSpirit
	{
		private string _strName = "";
		private string _strAdvantages = "";

		#region Properties
		/// <summary>
		/// Name of the Mentor Spirit or Paragon.
		/// </summary>
		public string Name
		{
			get
			{
				return _strName;
			}
			set
			{
				_strName = value;
			}
		}

		/// <summary>
		/// Advantages and Disadvantages that the Mentor Spirit or Paragon grants.
		/// </summary>
		public string Advantages
		{
			get
			{
				return _strAdvantages;
			}
			set
			{
				_strAdvantages = value;
			}
		}
		#endregion
	}
}

