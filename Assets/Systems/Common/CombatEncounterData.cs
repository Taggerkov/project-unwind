using Systems.Combat.Combatant.Data;
using Systems.Stage;
using UnityEngine.AddressableAssets;

namespace Systems.Common
{
    public struct CombatEncounterData
    {
        public AssetReferenceT<CombatantDataSO> Combatant0;
        public AssetReferenceT<CombatantDataSO> Combatant1;

        public StageEntry Stage;
    }
}