using System;
using UnityEngine;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Apply to a [SerializeReference] CombatantMove field to get a single-line
    /// type-selector dropdown in the inspector that is filtered to types compatible
    /// with the sibling StatsTemplate field on the same object.
    ///
    /// Unlike [StatsConstrained], this attribute targets standalone fields rather than
    /// lists, and it does not expand child properties — the selection line is all you get.
    /// Use it for the common-move slots (CmnActStand, CmnActFWalk, …) where the move
    /// is wired up by code and its fields don't need to be edited per-character.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class StatsConstrainedSelector : PropertyAttribute
    {
    }
}