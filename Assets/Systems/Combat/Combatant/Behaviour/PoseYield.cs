using System;

namespace Systems.Combat.Combatant.Behaviour
{
    /// <summary>
    /// Returned from CombatantMove.Pose() and yielded inside Script().
    /// Tells the MoveRunner to hold this pose for <see cref="Ticks"/> frames
    /// before resuming script execution.
    /// </summary>
    public sealed class PoseYield
    {
        public readonly uint Id;
        public readonly int Ticks;

        public PoseYield(uint id, int ticks)
        {
            Id = id;
            Ticks = ticks;
        }

        public uint CollectionId => Id / 100;
        public uint PoseId => Id % 100;
    }
}