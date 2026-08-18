using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chummer.Core
{
    public sealed class CharacterStackedFocusData
    {
        internal CharacterStackedFocusData(string strGuid, string strGearId, bool blnBonded,
            bool blnCompositeGearExists, string strCompositeGearName,
            IReadOnlyList<CharacterStackedFocusGearData> lstComponents)
        {
            Guid = strGuid;
            GearId = strGearId;
            Bonded = blnBonded;
            CompositeGearExists = blnCompositeGearExists;
            CompositeGearName = strCompositeGearName;
            Components = lstComponents;
        }

        public string Guid { get; }
        public string GearId { get; }
        public bool Bonded { get; }
        public bool CompositeGearExists { get; }
        public string CompositeGearName { get; }
        public IReadOnlyList<CharacterStackedFocusGearData> Components { get; }
        public int TotalForce => Components.Sum(component => int.TryParse(component.Rating,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out int intRating) ? intRating : 0);
        public string DisplayName => string.Join(", ", Components.Select(component => component.Name));
    }

}
