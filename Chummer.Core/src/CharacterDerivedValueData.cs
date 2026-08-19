namespace Chummer.Core
{
    public sealed class CharacterDerivedValueData
    {
        internal CharacterDerivedValueData(int intValue, string strTooltip)
        {
            Value = intValue;
            Tooltip = strTooltip;
        }

        public int Value { get; }
        public string Tooltip { get; }
    }

}
