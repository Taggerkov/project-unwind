using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Input;
using UnityEngine;

namespace Systems.CPU
{
    /// <summary>
    /// Describes one move the AI may attempt, including when it's viable (range),
    /// how to execute it (button + motion), and how often it can be used (cooldown).
    ///
    /// Analogous to the ArcSys per-move CPU cluster:
    ///   cpuEstimateAttackBox → RangeMin / RangeMax   (simplified to 1D horizontal distance)
    ///   cpuEstimatePoint     → Priority
    ///   cpuEstimateInterval  → CooldownTicks
    /// </summary>
    [Serializable]
    public class CpuMoveHintEntry
    {
        [TypeSelector(false), SerializeReference]
        [Tooltip("Exact type name of the CombatantMove subclass (e.g. \"SolFaust\", \"SolGunFlame\"). " +
                 "Used only for editor readability — the AI selects moves by range+priority, not by name.")]
        public CombatantMove MoveType;

        [Tooltip("Which button to press when executing this move.")]
        public EButtonInput Button = EButtonInput.Light;

        [Tooltip("Motion input to perform before pressing the button. " +
                 "None = instant button press (normals, held-direction moves). " +
                 "Anything else queues the appropriate direction sequence in AIMotionPlayer.")]
        public EMotionInput RequiredMotion = EMotionInput.None;

        [Header("Range (world units, horizontal distance to opponent)")]
        [Tooltip("Minimum distance at which this move is viable. " +
                 "Set > 0 to prevent the AI from using a far-reaching move up close.")]
        [Min(0f)]
        public float RangeMin = 0f;

        [Tooltip("Maximum distance at which this move is viable. " +
                 "Analogous to the width of cpuEstimateAttackBox.")]
        [Min(0f)]
        public float RangeMax = 2f;

        [Header("Selection")]
        [Range(0, 100)]
        [Tooltip("When multiple moves are in range, the one with the highest Priority wins. " +
                 "Ties are broken by list order. Analogous to cpuEstimatePoint.")]
        public int Priority = 50;

        [Tooltip("Minimum ticks between uses of this move. " +
                 "Analogous to cpuEstimateInterval.")]
        [Min(0)]
        public int CooldownTicks = 60;

        // ── Runtime ───────────────────────────────────────────────────────────────────

        /// <summary>Counts down each tick; the move is not a candidate while > 0.</summary>
        [NonSerialized] public int RemainingCooldown;
    }

    /// <summary>
    /// Asset assigned to a combatant to define its AI attack repertoire.
    /// One sheet covers all moves for one character.
    /// </summary>
    [CreateAssetMenu(fileName = "AIMoveHintSheet", menuName = "Unwind Database/Combat/AI Move Hint Sheet")]
    public class CpuMoveHintSheet : ScriptableObject
    {
        [SerializeField] private List<CpuMoveHintEntry> _entries = new();

        /// <summary>All hint entries for this character.</summary>
        public IReadOnlyList<CpuMoveHintEntry> Entries => _entries;

        /// <summary>Returns the first entry matching <paramref name="moveTypeName"/>, or null.</summary>
        public CpuMoveHintEntry FindHint(CombatantMove moveTypeName)
        {
            return _entries.FirstOrDefault(e => e.MoveType.GetType() == moveTypeName.GetType());
        }
    }
}