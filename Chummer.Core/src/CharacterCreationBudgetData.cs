using System.Collections.Generic;

namespace Chummer.Core
{
    public sealed class CharacterCreationBudgetData
    {
        internal CharacterCreationBudgetData(string strBuildMethod, int intStarting, int intRemaining, int intSpent,
            IReadOnlyList<CharacterCreationBudgetCategoryData> lstCategories)
        {
            BuildMethod = strBuildMethod;
            Starting = intStarting;
            Remaining = intRemaining;
            Spent = intSpent;
            Categories = lstCategories;
        }

        public string BuildMethod { get; }
        public int Starting { get; }
        public int Remaining { get; }
        public int Spent { get; }
        public IReadOnlyList<CharacterCreationBudgetCategoryData> Categories { get; }
    }

}
