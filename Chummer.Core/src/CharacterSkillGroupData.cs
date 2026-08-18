namespace Chummer.Core
{
    public sealed class CharacterSkillGroupData
    {
        internal CharacterSkillGroupData(string strName, string strRating, bool blnBroken)
        {
            Name = strName;
            Rating = strRating;
            Broken = blnBroken;
        }

        public string Name { get; private set; }
        public string Rating { get; private set; }

        /// <summary>Whether the BreakSkillGroupsInCreateMode house rule has unlocked this group's
        /// member Skills to be raised individually - see <see cref="CharacterFileService.BreakSkillGroup"/>.</summary>
        public bool Broken { get; private set; }
    }

}
