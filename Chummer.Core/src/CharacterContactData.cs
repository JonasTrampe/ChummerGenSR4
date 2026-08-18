namespace Chummer.Core
{
    public sealed class CharacterContactData
    {
        internal CharacterContactData(int intContactId, string strName, string strConnection, string strLoyalty,
            bool blnIsEnemy, string strNotes, bool blnFree, string strGroupName, int intMembership,
            int intAreaOfInfluence, int intMagicalResources, int intMatrixResources, string strFileName = "",
            string strRelativeFileName = "")
        {
            ContactId = intContactId;
            Name = strName;
            Connection = strConnection;
            Loyalty = strLoyalty;
            IsEnemy = blnIsEnemy;
            Notes = strNotes;
            Free = blnFree;
            GroupName = strGroupName;
            Membership = intMembership;
            AreaOfInfluence = intAreaOfInfluence;
            MagicalResources = intMagicalResources;
            MatrixResources = intMatrixResources;
            FileName = strFileName;
            RelativeFileName = strRelativeFileName;
        }

        public int ContactId { get; }
        public string Name { get; }
        public string Connection { get; }
        public string Loyalty { get; }
        public bool IsEnemy { get; }
        public string Notes { get; }

        /// <summary>Doesn't cost Karma/BP to add - matches the legacy "Free" checkbox.</summary>
        public bool Free { get; }

        /// <summary>Free-text profession/organization label for a Group contact (e.g. "Hacker").</summary>
        public string GroupName { get; }

        public int Membership { get; }
        public int AreaOfInfluence { get; }
        public int MagicalResources { get; }
        public int MatrixResources { get; }

        /// <summary>Absolute path of a character file linked to this contact, when available.</summary>
        public string FileName { get; }

        /// <summary>Path of the linked character relative to the application directory.</summary>
        public string RelativeFileName { get; }

        /// <summary>Sum of the four Group modifiers - adds to Connection+Loyalty in the cost
        /// formula, ported from frmSelectContactConnection.cs's Total Connection Modifier.</summary>
        public int GroupRating => Membership + AreaOfInfluence + MagicalResources + MatrixResources;
    }

}
