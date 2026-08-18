namespace Chummer.Core
{
    public sealed class CharacterCommlinkData
    {
        internal CharacterCommlinkData(string strGuid, string strName, int intResponse, bool blnEquipped, bool blnActive)
        {
            Guid = strGuid;
            Name = strName;
            Response = intResponse;
            Equipped = blnEquipped;
            Active = blnActive;
        }

        public string Guid { get; }
        public string Name { get; }
        public int Response { get; }
        public bool Equipped { get; }
        public bool Active { get; }
    }
}
