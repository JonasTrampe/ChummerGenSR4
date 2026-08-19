namespace Chummer.Core
{
    public sealed class CharacterCritterPowerData
    {
        internal CharacterCritterPowerData(string strGuid, string strName, string strExtra, string strPoints,
            string strRating = "0", string strNotes = "", bool blnCountsTowardsLimit = true)
        {
            Guid = strGuid;
            Name = strName;
            Extra = strExtra;
            Points = strPoints;
            Rating = strRating;
            Notes = strNotes;
            CountsTowardsLimit = blnCountsTowardsLimit;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Extra { get; }
        public string Points { get; }

        /// <summary>Ported from clsUnique.cs's CritterPower.Rating - only meaningful for powers
        /// whose rules-data entry sets &lt;rating&gt;yes&lt;/rating&gt; (e.g. Armor (Ballistic));
        /// "0" for every other power.</summary>
        public string Rating { get; }
        public string Notes { get; }
        public bool CountsTowardsLimit { get; }
        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
    }

}
