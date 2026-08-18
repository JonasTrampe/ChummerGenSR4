namespace Chummer.Core
{
    public sealed class CharacterSpiritData
    {
        internal CharacterSpiritData(string strName, string strCritterName, string strServices, string strForce,
            bool blnBound, string strType, int intSpiritId, string strNotes)
        {
            Name = strName;
            CritterName = strCritterName;
            Services = strServices;
            Force = strForce;
            Bound = blnBound;
            Type = strType;
            SpiritId = intSpiritId;
            Notes = strNotes;
        }

        public string Name { get; }
        public string CritterName { get; }
        public string Services { get; }
        public string Force { get; }
        public bool Bound { get; }
        public string Type { get; }
        public int SpiritId { get; }
        public string Notes { get; }

        public string DisplayName => string.IsNullOrEmpty(CritterName) ? Name : Name + " (" + CritterName + ")";
    }

}
