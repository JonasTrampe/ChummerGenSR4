using System.Globalization;

namespace Chummer.Core
{
    public sealed class CharacterPowerData
    {
        internal CharacterPowerData(string strName, string strExtra, string strRating, string strPointsPerLevel,
            string strDiscountedAdeptWay, string strDiscountedGeas, int intPowerId, string strNotes)
        {
            Name = strName;
            Extra = strExtra;
            Rating = strRating;
            PointsPerLevel = strPointsPerLevel;
            DiscountedAdeptWay = bool.TryParse(strDiscountedAdeptWay, out var blnDiscountedAdeptWay)
                && blnDiscountedAdeptWay;
            DiscountedGeas = bool.TryParse(strDiscountedGeas, out var blnDiscountedGeas)
                && blnDiscountedGeas;
            PowerId = intPowerId;
            Notes = strNotes;
        }

        public string Name { get; }
        public string Extra { get; }
        public string Rating { get; }
        public string PointsPerLevel { get; }
        public bool DiscountedAdeptWay { get; }
        public bool DiscountedGeas { get; }
        public int PowerId { get; }
        public string Notes { get; }

        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";

        public decimal Discount
        {
            get
            {
                if (!DiscountedAdeptWay && !DiscountedGeas)
                    return 1.0m;

                decimal decMultiplier = 1.0m;
                if (DiscountedAdeptWay)
                    decMultiplier -= 0.25m;
                if (DiscountedGeas)
                    decMultiplier -= 0.25m;
                return decMultiplier;
            }
        }

        public string CalculatedPointsPerLevel
        {
            get
            {
                if (!decimal.TryParse(PointsPerLevel, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decPerLevel))
                    return PointsPerLevel;

                return (decPerLevel * Discount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public string TotalPoints
        {
            get
            {
                if (!decimal.TryParse(CalculatedPointsPerLevel, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decPerLevel)
                    || !decimal.TryParse(Rating, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var decRating))
                    return PointsPerLevel;
                return (decPerLevel * decRating).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

}
