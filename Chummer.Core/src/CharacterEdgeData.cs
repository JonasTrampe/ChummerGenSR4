namespace Chummer.Core
{
    public sealed class CharacterEdgeData
    {
        internal CharacterEdgeData(int intRemaining, int intMaximum)
        {
            Remaining = intRemaining;
            Maximum = intMaximum;
        }

        public int Remaining { get; }
        public int Maximum { get; }
    }

}
