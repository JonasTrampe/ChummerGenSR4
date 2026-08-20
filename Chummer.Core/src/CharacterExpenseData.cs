using System;
using System.Globalization;

namespace Chummer.Core
{
    public sealed class CharacterExpenseData
    {
        internal CharacterExpenseData(string strGuid, string strDate, string strAmount, string strReason, bool blnRefund)
        {
            Guid = strGuid;
            Date = strDate;
            Amount = strAmount;
            Reason = strReason;
            Refund = blnRefund;
        }

        /// <summary>Stable identity for UpdateExpense/RemoveExpense - assigned by AddExpense at
        /// creation time. Empty for entries saved before this field existed.</summary>
        public string Guid { get; }

        public string Date { get; }
        public string Amount { get; }
        public string Reason { get; }
        public bool Refund { get; }

        public string DisplayDate =>
            DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var datValue)
                ? datValue.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                : Date;
    }

}
