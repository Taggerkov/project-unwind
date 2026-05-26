using System;
using System.Collections.Generic;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Serializable wrapper around a <c>List&lt;CombatantMove&gt;</c> stored as
    /// <c>[SerializeReference]</c> so Unity can persist polymorphic move subclasses
    /// in ScriptableObjects without breaking on type changes.
    /// </summary>
    [Serializable]
    public class CombatantMoveList
    {
        /// <summary>The ordered list of move instances; each element may be a different <see cref="CombatantMove"/> subclass.</summary>
        [SerializeReference] public List<CombatantMove> list = new();
    }
}