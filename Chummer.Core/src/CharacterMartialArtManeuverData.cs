namespace Chummer.Core
{
    public sealed class CharacterMartialArtManeuverData
    {
        internal CharacterMartialArtManeuverData(string strName, int intManeuverId, string strNotes)
        {
            Name = strName;
            ManeuverId = intManeuverId;
            Notes = strNotes;
        }

        public string Name { get; }
        public int ManeuverId { get; }
        public string Notes { get; }
    }

}
