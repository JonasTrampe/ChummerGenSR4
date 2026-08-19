using System.Collections.Generic;

namespace Chummer.Core
{
    public sealed class CharacterComplexFormData
    {
        internal CharacterComplexFormData(string strGuid, string strName, string strCategory, string strExtra,
            string strRating, IReadOnlyList<(string Name, string Rating)> lstOptions, string strNotes)
        {
            Guid = strGuid;
            Name = strName;
            Category = strCategory;
            Extra = strExtra;
            Rating = strRating;
            Options = lstOptions;
            Notes = strNotes;
        }

        public string Guid { get; }
        public string Name { get; }
        public string Category { get; }
        public string Extra { get; }
        public string Rating { get; }
        public IReadOnlyList<(string Name, string Rating)> Options { get; }
        public string Notes { get; }
        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
    }

}
