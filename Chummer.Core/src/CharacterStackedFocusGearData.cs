namespace Chummer.Core
{
    public sealed class CharacterStackedFocusGearData
    {
        internal CharacterStackedFocusGearData(string strGuid, string strName, string strCategory, string strRating)
        {
            Guid = strGuid;
            Name = strName;
            Category = strCategory;
            Rating = strRating;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Category { get; }
        public string Rating { get; }
    }

}
