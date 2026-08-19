namespace Chummer.Core
{
    public sealed class CharacterQualityData
    {
        internal CharacterQualityData(string strName, string strExtra, string strType, int intQualityId,
            string strNotes, QualitySource eSource = QualitySource.Selected)
        {
            Name = strName;
            Extra = strExtra;
            Type = strType;
            QualityId = intQualityId;
            Notes = strNotes;
            Source = eSource;
        }

        public string Name { get; }
        public string Extra { get; }
        public string Type { get; private set; }
        public int QualityId { get; }
        public string Notes { get; }
        public QualitySource Source { get; }

        public string DisplayName => string.IsNullOrEmpty(Extra) ? Name : Name + " (" + Extra + ")";
    }

    /// <summary>Substitutes "Rating" into a rules-data cost/avail/essence formula (e.g.
    /// "Rating * 3000") and evaluates it, same technique as legacy clsEquipment.cs. Shared by
    /// <see cref="CharacterTreeItemData"/>'s CalculatedCost/CalculatedAvail (post-save, resolved
    /// from the saved character) and any picker UI that needs to preview the value for a rating
    /// the user hasn't committed to yet.</summary>
}
