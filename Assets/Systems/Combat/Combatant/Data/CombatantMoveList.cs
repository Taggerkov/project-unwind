using System;
using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems.Combat.Combatant.Data
{
    [Serializable]
    public class CombatantMoveList
    {
        [SerializeReference] public List<CombatantMove> list = new();
    }
}