using Systems.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Systems.Combat.Combatant.Data
{
    [CreateAssetMenu(fileName = "CombatantDataSO", menuName = "Unwind Database/Combatant Data", order = 0)]
    public class CombatantDataSO : ScriptableObject
    {
        public string combatantCode;
        public string combatantName;
        public AssetReferenceGameObject combatantPrefabReference;
        public CinematicCameraSequence[] cinematicCameraSequences;
    }
}
