namespace Chummer.Core
{
    public sealed class CharacterMetamagicData
    {
        internal CharacterMetamagicData(string strGuid, string strName, string strSource, string strPage,
            string strNotes)
        {
            Guid = strGuid;
            Name = strName;
            Source = strSource;
            Page = strPage;
            Notes = strNotes;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Source { get; }
        public string Page { get; }
        public string Notes { get; }
        public string SourcePage => string.IsNullOrWhiteSpace(Page) ? Source : Source + " " + Page;
    }

}
