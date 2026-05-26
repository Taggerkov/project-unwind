using Systems.Combat.Combatant.Data;
using Systems.Stage;
using UnityEngine.AddressableAssets;

namespace Systems.Common
{
    /// <summary>
    /// Lightweight value type that bundles the Addressable references needed to start a combat session:
    /// data assets for both combatants and the target stage. Passed to <c>GameManager.BeginCombat</c>.
    /// </summary>
    public struct CombatEncounterData
    {
        /// <summary>Addressable reference to the first combatant's data asset.</summary>
        public AssetReferenceT<CombatantDataSO> Combatant0;

        /// <summary>Addressable reference to the second combatant's data asset.</summary>
        public AssetReferenceT<CombatantDataSO> Combatant1;

        /// <summary>Addressable reference to the stage entry that defines the scene and spawn layout.</summary>
        public AssetReferenceT<StageEntrySO> Stage;
    }
}