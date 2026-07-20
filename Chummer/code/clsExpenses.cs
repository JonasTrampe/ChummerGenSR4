using System;
using System.Xml;

namespace Chummer
{
	/// <summary>
	/// Exense Log Entry.
	/// </summary>
	public class ExpenseLogEntry
	{
		private Guid _guiID = new Guid();
		private DateTime _datDate = new DateTime();
		private int _intAmount = 0;
		private string _strReason = "";
		private ExpenseType _objExpenseType;
		private bool _blnRefund = false;
		private ExpenseUndo _objUndo;

		#region Helper Methods
		/// <summary>
		/// ExpenseLogEntry Comparer.
		/// </summary>
		public static int CompareDate(ExpenseLogEntry x, ExpenseLogEntry y)
		{
			if (x == null)
			{
				if (y == null)
					return 0;
				else
					return -1;
			}
			else
			{
				if (y == null)
					return 1;
				else
				{
					int intReturn = y.Date.CompareTo(x.Date);
					return intReturn;
				}
			}
		}

		/// <summary>
		/// Convert a string to an ExpenseType.
		/// </summary>
		/// <param name="strValue">String value to convert.</param>
		public ExpenseType ConvertToExpenseType(string strValue)
		{
			switch (strValue)
			{
				case "Nuyen":
					return ExpenseType.Nuyen;
				default:
					return ExpenseType.Karma;
			}
		}
		#endregion

		#region Constructor, Create, Save, Load, and Print Methods
		public ExpenseLogEntry()
		{
			_guiID = Guid.NewGuid();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, null);
		}

		/// <summary>
		/// Create a new Expense Log Entry.
		/// </summary>
		/// <param name="intKarma">Amount of the Karma/Nuyen expense.</param>
		/// <param name="strReason">Reason for the Karma/Nueyn change.</param>
		/// <param name="objExpenseType">Type of expense, either Karma or Nuyen.</param>
		/// <param name="datDate">Date and time of the Expense.</param>
		/// <param name="blnRefund">Whether or not this expense is a Karma refund.</param>
		public void Create(int intKarma, string strReason, ExpenseType objExpenseType, DateTime datDate, bool blnRefund = false)
		{
			if (blnRefund)
				strReason += " (" + LanguageManager.Instance.GetString("String_Expense_Refund") + ")";
			_intAmount = intKarma;
			_strReason = strReason;
			_datDate = datDate;
			_objExpenseType = objExpenseType;
			_blnRefund = blnRefund;
		}

		/// <summary>
		/// Save the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Save(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("expense");
			objWriter.WriteElementString("guid", _guiID.ToString());
			objWriter.WriteElementString("date", _datDate.ToString("s"));
			objWriter.WriteElementString("amount", _intAmount.ToString());
			objWriter.WriteElementString("reason", _strReason);
			objWriter.WriteElementString("type", _objExpenseType.ToString());
			objWriter.WriteElementString("refund", _blnRefund.ToString());
			if (_objUndo != null)
				_objUndo.Save(objWriter);
			objWriter.WriteEndElement();
		}

		/// <summary>
		/// Load the KarmaLogEntry from the XmlNode.
		/// </summary>
		/// <param name="objNode">XmlNode to load.</param>
		public void Load(XmlNode objNode)
		{
			_guiID = Guid.Parse(objNode["guid"].InnerText);
			_datDate = DateTime.Parse(objNode["date"].InnerText, GlobalOptions.Instance.CultureInfo);
			_intAmount = Convert.ToInt32(objNode["amount"].InnerText);
			_strReason = objNode["reason"].InnerText;
			_objExpenseType = ConvertToExpenseType(objNode["type"].InnerText);
			try
			{
				_blnRefund = Convert.ToBoolean(objNode["refund"].InnerText);
			}
			catch
			{
			}
			try
			{
				if (objNode["undo"] != null)
				{
					_objUndo = new ExpenseUndo();
					_objUndo.Load(objNode["undo"]);
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Print the object's XML to the XmlWriter.
		/// </summary>
		/// <param name="objWriter">XmlTextWriter to write with.</param>
		public void Print(XmlTextWriter objWriter)
		{
			objWriter.WriteStartElement("expense");
			objWriter.WriteElementString("date", _datDate.ToString());
			objWriter.WriteElementString("amount", _intAmount.ToString());
			objWriter.WriteElementString("reason", _strReason);
			objWriter.WriteElementString("type", _objExpenseType.ToString());
			objWriter.WriteElementString("refund", _blnRefund.ToString());
			objWriter.WriteEndElement();
		}
		#endregion

		#region Properties
		/// <summary>
		/// Internal identifier which will be used to identify this Expense Log Entry.
		/// </summary>
		public string InternalId
		{
			get
			{
				return _guiID.ToString();
			}
			set
			{
				_guiID = Guid.Parse(value);
			}
		}

		/// <summary>
		/// Date the Exense Log Entry was made.
		/// </summary>
		public DateTime Date
		{
			get
			{
				return _datDate;
			}
			set
			{
				_datDate = value;
			}
		}

		/// <summary>
		/// Karma/Nuyen amount gained or spent.
		/// </summary>
		public int Amount
		{
			get
			{
				return _intAmount;
			}
			set
			{
				_intAmount = value;
			}
		}

		/// <summary>
		/// The Reason for the Entry expense.
		/// </summary>
		public string Reason
		{
			get
			{
				return _strReason;
			}
			set
			{
				_strReason = value;
			}
		}

		/// <summary>
		/// The Expense type.
		/// </summary>
		public ExpenseType Type
		{
			get
			{
				return _objExpenseType;
			}
			set
			{
				_objExpenseType = value;
			}
		}

		/// <summary>
		/// Whether or not the Expense is a Karma refund.
		/// </summary>
		public bool Refund
		{
			get
			{
				return _blnRefund;
			}
			set
			{
				_blnRefund = value;
			}
		}

		/// <summary>
		/// Undo object.
		/// </summary>
		public ExpenseUndo Undo
		{
			get
			{
				return _objUndo;
			}
			set
			{
				_objUndo = value;
			}
		}
		#endregion
	}
}
