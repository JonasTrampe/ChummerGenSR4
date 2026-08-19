using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Chummer.Core
{
    public static class RatingExpression
    {
        public static double Evaluate(string strExpression, string strRating)
        {
            if (string.IsNullOrEmpty(strExpression)) return 0;
            var strSubstituted = strExpression.Replace("Rating", strRating);
            try
            {
                var objNavigator = new System.Xml.XPath.XPathDocument(new StringReader("<i/>")).CreateNavigator();
                var objExpression = objNavigator.Compile(strSubstituted);
                return Convert.ToDouble(objNavigator.Evaluate(objExpression), CultureInfo.InvariantCulture);
            }
            catch
            {
                return double.TryParse(strSubstituted, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblValue)
                    ? dblValue
                    : 0;
            }
        }
    }

}
