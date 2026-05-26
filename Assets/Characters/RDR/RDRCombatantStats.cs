using System;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;

namespace Characters.RDR
{
    /// <summary>
    /// Redeemer (RDR) specific combatant stats. Extends the base stat block with the Bulwark stance
    /// flag and an air movement action counter used to gate aerial dashes and double-jumps.
    /// </summary>
    [Serializable]
    public class RDRCombatantStats : CombatantStats
    {
        /// <summary>Maximum number of air movement actions (dashes, double-jumps) available per airborne state.</summary>
        [SerializeField] public int MaxAirMovementActions = 1;

        /// <summary>Whether Bulwark stance is currently active; toggled by stance moves.</summary>
        [NonSerialized] public bool isInBulkwarkStance;

        /// <summary>Number of air movement actions consumed since the last landing; reset by <see cref="ResetAirMovementActions"/>.</summary>
        [NonSerialized] private int airMovementActionsTaken;

        /// <summary>True when at least one air movement action is still available this airborne state.</summary>
        public bool CanTakeAirMovementAct => airMovementActionsTaken < MaxAirMovementActions;

        /// <summary>Consumes one air movement action.</summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="CanTakeAirMovementAct"/> is false.</exception>
        public void UseAirMovementAction()
        {
            if (!CanTakeAirMovementAct) throw new InvalidOperationException("No jumps remaining!");
            airMovementActionsTaken++;
        }

        /// <summary>Resets the air movement action counter; called on landing.</summary>
        public void ResetAirMovementActions() => airMovementActionsTaken = 0;

        /// <inheritdoc/>
        public override CombatantStats Clone() => (RDRCombatantStats)MemberwiseClone();
    }
}