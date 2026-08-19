namespace Chummer.Core
{
    public sealed class CharacterInitiationGradeData
    {
        internal CharacterInitiationGradeData(string strGrade, bool blnGroup, bool blnOrdeal, bool blnTechnomancer)
        {
            Grade = strGrade;
            Group = blnGroup;
            Ordeal = blnOrdeal;
            Technomancer = blnTechnomancer;
        }

        public string Grade { get; }
        public bool Group { get; }
        public bool Ordeal { get; }
        public bool Technomancer { get; }

        public string DisplayName
        {
            get
            {
                var strLabel = (Technomancer ? "Submersion" : "Initiatengrad") + " " + Grade;
                if (Ordeal) strLabel += " (Prüfung)";
                if (Group) strLabel += " (Gruppe)";
                return strLabel;
            }
        }
    }

}
