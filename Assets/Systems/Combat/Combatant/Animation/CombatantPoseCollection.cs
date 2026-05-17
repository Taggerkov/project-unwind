using AYellowpaper.SerializedCollections;
using UnityEngine;
using Pose = Systems.Combat.Combatant.Behaviour.Pose;

namespace Systems.Combat.Combatant.Animation
{
    [CreateAssetMenu(fileName = "NAME_000", menuName = "Unwind Databse/Combatant/Combatant Pose Collection")]
    public class CombatantPoseCollection : ScriptableObject
    {
        public SerializedDictionary<uint, Pose> poses;
    }
}