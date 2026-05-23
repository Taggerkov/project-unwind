using System;
using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using UnityEngine;

namespace Systems.CPU
{
    public enum EDefenceResponse
    {
        Ignore, // do nothing
        Guard, // hold back
        CounterMove, // punish with a specific move on our sheet
    }

    [Serializable]
    public class CpuDefenceHintEntry
    {
        [TypeSelector(false), SerializeReference]
        [Tooltip("Type name of the opponent move this entry responds to. " +
                 "Leave empty to use this as a catch-all fallback.")]
        public CombatantMove OpponentMoveType;

        public EDefenceResponse Response = EDefenceResponse.Ignore;

        [TypeSelector(false), SerializeReference]
        [Tooltip("Only used when Response == CounterMove. " +
                 "Must match a valid entry in our own CpuMoveHintSheet.")]
        public CombatantMove CounterMoveType;

        [Range(0, 100)] [Tooltip("Higher priority entries win when multiple entries match the same move.")]
        public int Priority = 50;
    }

    [CreateAssetMenu(fileName = "CpuDefenceHintSheet", menuName = "Unwind Database/Combat/CPU Defence Hint Sheet")]
    public class CpuDefenceHintSheet : ScriptableObject
    {
        [SerializeField] private List<CpuDefenceHintEntry> _entries = new();

        public IReadOnlyList<CpuDefenceHintEntry> Entries => _entries;

        /// <summary>
        /// Returns the highest-priority entry matching <paramref name="opponentMoveTypeName"/>,
        /// falling back to a catch-all entry (empty OpponentMoveTypeName) if no exact match exists.
        /// Returns null if no entry applies.
        /// </summary>
        public CpuDefenceHintEntry FindBestResponse(CombatantMove opponentMove)
        {
            CpuDefenceHintEntry best = null;
            CpuDefenceHintEntry catchAll = null;
            int bestPri = -1;

            foreach (var entry in _entries)
            {
                // Null OpponentMoveType marks an explicit catch-all.
                if (entry.OpponentMoveType == null)
                {
                    if (catchAll == null || entry.Priority > catchAll.Priority)
                        catchAll = entry;
                    continue;
                }

                if (entry.OpponentMoveType.GetType() != opponentMove.GetType()) continue;
                if (entry.Priority <= bestPri) continue;

                best = entry;
                bestPri = entry.Priority;
            }

            // Null = completely unknown move, not the same as an Ignore entry.
            return best ?? catchAll;
        }
    }
}