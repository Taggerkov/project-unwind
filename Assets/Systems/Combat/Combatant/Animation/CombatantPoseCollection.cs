using AYellowpaper.SerializedCollections;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    /// <summary>
    /// ScriptableObject holding a dictionary of <see cref="Pose"/> frames keyed by local pose ID.
    /// Owned by a <see cref="CombatantPoseSheet"/> which assigns it a collection ID and manages
    /// the global ID space (collectionId * 100 + poseId).
    /// </summary>
    [CreateAssetMenu(fileName = "NAME_000", menuName = "Unwind/Combatant/Combatant Pose Collection")]
    public class CombatantPoseCollection : ScriptableObject
    {
        /// <summary>All poses in this collection, keyed by their local pose index.</summary>
        public SerializedDictionary<uint, Pose> poses;
    }
}