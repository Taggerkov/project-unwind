using UnityEngine;

namespace Systems.Stage
{
    /// <summary>
    /// MonoBehaviour placed in a stage scene to designate the world-space spawn transforms for
    /// both combatants. The <see cref="CombatManager"/> reads <see cref="Combatant0SpawnPoint"/>
    /// and <see cref="Combatant1SpawnPoint"/> when positioning characters at round start.
    /// </summary>
    public class CombatantSpawnMarker : MonoBehaviour
    {
        /// <summary>Scene GameObject whose transform is used as the spawn position for combatant 0.</summary>
        [SerializeField] private GameObject combatant0SpawnMarker;

        /// <summary>Scene GameObject whose transform is used as the spawn position for combatant 1.</summary>
        [SerializeField] private GameObject combatant1SpawnMarker;

        /// <summary>Display label for the combatant 0 spawn point; auto-populated from the marker's name.</summary>
        [SerializeField] public string combatant0SpawnMarkerLabel;

        /// <summary>Display label for the combatant 1 spawn point; auto-populated from the marker's name.</summary>
        [SerializeField] public string combatant1SpawnMarkerLabel;

        /// <summary>World-space transform for combatant 0's spawn position, or null when unassigned.</summary>
        public Transform Combatant0SpawnPoint => combatant0SpawnMarker ? combatant0SpawnMarker.transform : null;

        /// <summary>World-space transform for combatant 1's spawn position, or null when unassigned.</summary>
        public Transform Combatant1SpawnPoint => combatant1SpawnMarker ? combatant1SpawnMarker.transform : null;

        /// <summary>Initialises display labels from the assigned marker names when the component is first added.</summary>
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

        /// <summary>Warns when either spawn marker is unassigned and keeps the display labels in sync with marker names.</summary>
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