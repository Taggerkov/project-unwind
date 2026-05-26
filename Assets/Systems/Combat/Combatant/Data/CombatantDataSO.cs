
using Systems.CPU;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Asset that binds a combatant's Addressable prefab to its CPU personality and hint sheets.
    /// Loaded at the start of a <see cref="Systems.Core.ResourceManagement.CombatSession"/> and
    /// kept alive for the session's duration.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatantDataSO", menuName = "Unwind Database/Combatant Data", order = 0)]
    public class CombatantDataSO : ScriptableObject
    {
        /// <summary>Short identifier used internally (e.g. "RDR").</summary>
        public string combatantCode;

        /// <summary>Human-readable display name for UI (e.g. "The Redeemer").</summary>
        public string combatantName;

        /// <summary>Addressable reference to the character prefab instantiated at session load.</summary>
        public AssetReferenceGameObject combatantPrefabReference;

        /// <summary>CPU decision-making personality weights for this character.</summary>
        public CpuPersonality cpuPersonality;

        /// <summary>CPU move-selection hint data for this character.</summary>
        public CpuMoveHintSheet cpuMoveHintSheet;

        /// <summary>CPU defence-selection hint data for this character.</summary>
        public CpuDefenceHintSheet cpuDefenceHintSheet;
    }
}
