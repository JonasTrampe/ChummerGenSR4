namespace Chummer.Core
{
    public sealed class CharacterFocusData
    {
        internal CharacterFocusData(string strGuid, string strName, string strGearId, string strRating,
            bool blnLinkedGearExists, string strGearCategory)
        {
            Guid = strGuid;
            Name = strName;
            GearId = strGearId;
            Rating = strRating;
            LinkedGearExists = blnLinkedGearExists;
            GearCategory = strGearCategory;
        }

        public string Guid { get; }
        public string Name { get; }
        public string GearId { get; }
        public string Rating { get; }
        public bool LinkedGearExists { get; }
        public string GearCategory { get; }
    }

}
