using System;
using UnityEngine;

namespace Systems.Combat.Combatant.Data
{
    /// <summary>
    /// Applied to any <c>CombatantMove</c> field or <c>List&lt;CombatantMove&gt;</c> field
    /// on a <c>CombatantMoveSetDefinition</c>.
    ///
    /// Tells the custom property drawer to read the sibling <c>StatsTemplate</c> field
    /// and restrict the type picker to <c>CombatantMove&lt;TStats&gt;</c> subclasses whose
    /// generic argument exactly matches the assigned template's concrete type.
    ///
    /// Elements or fields whose assigned type no longer matches (e.g. after swapping
    /// the StatsTemplate) are highlighted with a warning rather than silently broken.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class StatsConstrainedListAttribute : PropertyAttribute { }
}