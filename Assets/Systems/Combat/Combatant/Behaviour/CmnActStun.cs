using System;
using Systems.Combat.Combatant.Animation;
using Systems.Combat.HitSystem;

namespace Systems.Combat.Combatant.Behaviour
{
    /// <summary>
    /// Abstract base for hitstun moves.
    /// Subclass this and implement Script() — read HitstunTicks and DamagePoseId()
    /// to hold the correct pose for the correct duration.
    ///
    /// Example:
    ///   protected override IEnumerator Script()
    ///   {
    ///       var id = HasDamagePoseOverride ? DamagePoseOverrideGlobalId : DamagePoseId();
    ///       yield return Pose(id, HitstunTicks);
    ///   }
    /// </summary>
    [Serializable]
    public abstract class CmnActHitstun : CombatantMove
    {
        // Never entered through the input system — only via CombatantBehaviour.StartMove().
        public override bool IsRegistered => false;

        /// <summary>Hitstun duration in ticks, written to Stats by CombatantBehaviour before this move starts.</summary>
        protected int HitstunTicks => Stats != null ? (int)Stats.PendingHitstunTicks : 0;

        /// <summary>The hit level that triggered this hitstun.</summary>
        protected EHitLevel HitLevel => Stats?.PendingHitLevel ?? EHitLevel.One;

        /// <summary>True when the attacker's HitData specified a custom damage pose.</summary>
        protected bool HasDamagePoseOverride => Stats?.PendingDamagePoseOverride ?? false;

        /// <summary>Global pose ID of the custom damage pose (valid only when HasDamagePoseOverride is true).</summary>
        protected uint DamagePoseOverrideGlobalId => Stats?.PendingDamagePoseOverrideId ?? 0;

        /// <summary>
        /// Returns the global pose ID for the damage pose at the current hit level.
        /// poseIndex selects a specific frame within the level's collection (0 = first/only pose).
        /// </summary>
        protected uint DamagePoseId(uint poseIndex = 0)
            => CombatantPoseSheet.GetDamagePoseGlobalId(HitLevel, poseIndex);
    }

    /// <summary>
    /// Abstract base for blockstun moves.
    /// Example:
    ///   protected override IEnumerator Script()
    ///   {
    ///       yield return Pose(BlockPoseId(), BlockstunTicks);
    ///   }
    /// </summary>
    [Serializable]
    public abstract class CmnActBlockstun : CombatantMove
    {
        public override bool IsRegistered => false;

        protected int BlockstunTicks => Stats != null ? (int)Stats.PendingBlockstunTicks : 0;
        protected EHitLevel HitLevel => Stats?.PendingHitLevel ?? EHitLevel.One;

        protected uint BlockPoseId(uint poseIndex = 0)
            => CombatantPoseSheet.GetBlockPoseGlobalId(HitLevel, poseIndex);
    }
}