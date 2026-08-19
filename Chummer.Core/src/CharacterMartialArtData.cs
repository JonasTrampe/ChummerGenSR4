using System.Collections.Generic;

namespace Chummer.Core
{
    public sealed class CharacterMartialArtData
    {
        internal CharacterMartialArtData(string strName, string strRating, string strSource, string strPage,
            IReadOnlyList<string> lstAdvantages, int intMartialArtId, string strNotes)
        {
            Name = strName;
            Rating = strRating;
            Source = strSource;
            Page = strPage;
            Advantages = lstAdvantages;
            MartialArtId = intMartialArtId;
            Notes = strNotes;
        }

        public string Name { get; }
        public string Rating { get; }
        public string Source { get; }
        public string Page { get; }
        public string SourcePage => string.IsNullOrWhiteSpace(Page) ? Source : Source + " " + Page;
        public IReadOnlyList<string> Advantages { get; }
        public int MartialArtId { get; }
        public string Notes { get; }
    }

}
