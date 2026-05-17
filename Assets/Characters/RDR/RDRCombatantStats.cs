using System;
using Systems.Combat.Combatant.Behaviour;

namespace Characters.RDR
{
    [Serializable]
    public class RDRCombatantStats : CombatantStats
    {

        public bool isInBulkwarkStance;
        public const int MaxJumps = 2;
        
        public int jumpsUsed;
        
        public bool CanJump => jumpsUsed < MaxJumps;
        
        public override CombatantStats Clone() => (RDRCombatantStats)MemberwiseClone();
    }

    [Serializable]
    public class TestCombatantStats : CombatantStats
    {
        public override CombatantStats Clone() => (TestCombatantStats)MemberwiseClone();
    }
}