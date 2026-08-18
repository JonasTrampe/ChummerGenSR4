namespace Chummer.Core
{
    public sealed class CharacterCreationBudgetCategoryData
    {
        internal CharacterCreationBudgetCategoryData(string strName, int intCost)
        {
            Name = strName;
            Cost = intCost;
        }

        public string Name { get; }
        public int Cost { get; }
    }

}
