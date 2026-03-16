using UnityEngine;

namespace Systems.Data
{
    [CreateAssetMenu(fileName = "CombatantDataSO", menuName = "Unwind Database/Combatant Data", order = 0)]
    public class CombatantDataSO : ScriptableObject
    {
        public string combatantCode;
        public string combatantName;
        public GameObject combatantPrefab;
        public CinematicCameraSequence[] cinematicCameraSequences;
    }
}
