namespace Chummer.Core
{
    public sealed class CharacterArmorCapacityData
    {
        internal CharacterArmorCapacityData(int intTotal, int intRemaining)
        {
            Total = intTotal;
            Remaining = intRemaining;
        }

        public int Total { get; }
        public int Remaining { get; }
    }

}
