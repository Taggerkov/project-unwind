using UnityEngine;

namespace Systems.Stage
{
    public class CombatantSpawnMarker : MonoBehaviour
    {
        [SerializeField] private GameObject combatant0SpawnMarker;
        [SerializeField] private GameObject combatant1SpawnMarker;

        [SerializeField] public string combatant0SpawnMarkerLabel;
        [SerializeField] public string combatant1SpawnMarkerLabel;

        public Transform Combatant0SpawnPoint => combatant0SpawnMarker ? combatant0SpawnMarker.transform : null;
        public Transform Combatant1SpawnPoint => combatant1SpawnMarker ? combatant1SpawnMarker.transform : null;


        private void Reset()
        {
            if (combatant0SpawnMarker)
            {
                combatant0SpawnMarkerLabel = combatant0SpawnMarker.name;
            }

            if (combatant1SpawnMarker)
            {
                combatant1SpawnMarkerLabel = combatant1SpawnMarker.name;
            }
        }

        private void OnValidate()
        {
            if (!combatant0SpawnMarker)
            {
                Debug.LogWarning("Combatant 0 spawn marker is not assigned.", this);
            }

            if (!combatant1SpawnMarker)
            {
                Debug.LogWarning("Combatant 1 spawn marker is not assigned.", this);
            }

            if (combatant0SpawnMarker)
            {
                combatant0SpawnMarkerLabel = combatant0SpawnMarker.name;
            }

            if (combatant1SpawnMarker)
            {
                combatant1SpawnMarkerLabel = combatant1SpawnMarker.name;
            }
        }
    }
}