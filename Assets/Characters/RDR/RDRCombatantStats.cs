using System;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;

namespace Characters.RDR
{
    [Serializable]
    public class RDRCombatantStats : CombatantStats
    {
        [SerializeField] public int MaxJumps = 2;

        [NonSerialized] public bool isInBulkwarkStance;

        [NonSerialized] private int jumpsUsed;

        public bool CanJump => jumpsUsed < MaxJumps;

        public void UseJump()
        {
            if (!CanJump) throw new InvalidOperationException("No jumps remaining!");
            jumpsUsed++;
        }
        
        public void ResetJumps() => jumpsUsed = 0;

        public override CombatantStats Clone() => (RDRCombatantStats)MemberwiseClone();
    }
}