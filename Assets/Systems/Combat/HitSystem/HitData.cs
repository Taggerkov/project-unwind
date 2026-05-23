using System;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.Combat.HitSystem
{
    public enum EHitLevel
    {
        One,
        Two,
        Three,
        Four,
        Five
    }

    public enum EAttackDirection
    {
        Self = 0, // attacker's facing defines forward
        Player = 1, // defender's facing defines forward
        SelfToEnemy = 2, // physical direction attacker → defender
        PlayerToEnemy = 3 // physical direction defender → attacker
    }

    public enum EHitResolution
    {
        Hit,
        Blocked,
        Armored
    }

    public enum EHitTarget
    {
        Enemy,
        Ally,
        Any
    }

    public struct HitData
    {
        public uint
            HitId; // Unique identifier for this hit. Used to avoid processing the same hit multiple times across multiple frames.

        public EHitLevel
            Level; //The level of this hit, used for scaling damage and hitstun/blockstun aswell as as clash resolution.

        public float Damage; // The amount of damage this hit will deal if it connects before scaling.

        public EGuardType
            GuardType; // The type of guard required to block this hit. If the victim's guard type matches this, the hit will be blocked instead of hitting.

        public EHitTarget
            HitTarget; // The type of target this hit can affect from the point of view of the perpetrator.

        public uint
            HitstunDuration; // The amount of hitstun (in ticks) that the victim will suffer if the hit connects and is not blocked.

        public uint
            BlockstunDuration; // The amount of blockstun (in ticks) that the victim will suffer if the hit is blocked.

        public uint
            HitstopDurationOnBlock; // The amount of hitstop (in ticks) that the game's logic will be frozen when the hit is blocked.

        public uint
            HitstopDurationOnHit; // The amount of hitstop (in ticks) that the game's logic will be frozen when the hit connects.

        public EAttackDirection AttackDirection; // how HitKnockback/BlockKnockback X is interpreted

        public bool
            IsLauncher; // Whether this hit should cause the victim to be launched into the air if it connects (allows the victim to unground themselves)

        public Vector2 HitKnockback; // The knockback applied to the victim when hit connects
        public Vector2 HitSelfKnockback; // The knockback applied to the perpetrator when hit connects
        public Vector2 BlockKnockback; // The knockback applied to the victim when the hit is blocked
        public Vector2 BlockSelfKnockback; // The knockback applied to the perpetrator when the hit is blocked

        /// <summary>
        /// When true, the victim is forced into DamagePoseOverrideId instead of the
        /// level-based damage pose. Use for grabs, cinematic supers, etc.
        /// Mirrors ArcSys's damageSprite override.
        /// </summary>
        public bool OverrideDamagePose;

        /// <summary>Global pose ID (collectionId * 100 + poseId) to use when OverrideDamagePose is true.</summary>
        public uint DamagePoseOverrideId;

        public uint ComboCounterIncrease; // The amount the combo counter will increase by if this hit connects.

        public static HitData LightAttack() => new()
        {
            Level = EHitLevel.One,
            Damage = 20f,
            HitstunDuration = 9,
            BlockstunDuration = 5,
            HitstopDurationOnHit = 5,
            HitstopDurationOnBlock = 2,
            GuardType = EGuardType.Any,
            HitTarget = EHitTarget.Enemy,
            AttackDirection = EAttackDirection.SelfToEnemy,
            HitKnockback = new Vector2(2.5f, 0f),
            BlockKnockback = new Vector2(1.0f, 0f),
            BlockSelfKnockback = new Vector2(-1.5f, 0f),
            ComboCounterIncrease = 1
        };

        public static HitData MediumAttack() => new()
        {
            Level = EHitLevel.Two,
            Damage = 35f,
            HitstunDuration = 16,
            BlockstunDuration = 8,
            HitstopDurationOnHit = 7,
            HitstopDurationOnBlock = 4,
            GuardType = EGuardType.Any,
            HitTarget = EHitTarget.Enemy,
            AttackDirection = EAttackDirection.SelfToEnemy,
            HitKnockback = new Vector2(3.5f, 0f),
            BlockKnockback = new Vector2(1.5f, 0f),
            BlockSelfKnockback = new Vector2(-3.5f, 0f),
            ComboCounterIncrease = 1
        };

        public static HitData HeavyAttack() => new()
        {
            Level = EHitLevel.Three,
            Damage = 50f,
            HitstunDuration = 25,
            BlockstunDuration = 12,
            HitstopDurationOnHit = 12,
            HitstopDurationOnBlock = 8,
            GuardType = EGuardType.Any,
            HitTarget = EHitTarget.Enemy,
            AttackDirection = EAttackDirection.SelfToEnemy,
            HitKnockback = new Vector2(5.0f, 0f),
            BlockKnockback = new Vector2(2.5f, 0f),
            BlockSelfKnockback = new Vector2(-4.5f, 0f),
            ComboCounterIncrease = 1
        };
    }

    public struct HitResult
    {
        public CombatantBehaviour Perpetrator;
        public CombatantBehaviour Victim;
        public EHitResolution Resolution;
        public HitData HitData;

        //These are pre-solved by the CombatManager and live in world-space.
        public Vector2 VictimKnockback;
        public Vector2 PerpetratorKnockback;
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Hit"/>. Clears active hit data when
    /// the using block exits so stale data never leaks into the next pose.
    /// </summary>
    public sealed class HitScope : IDisposable
    {
        private readonly MoveRunner _runner;
        internal HitScope(MoveRunner runner) => _runner = runner;
        public void Dispose() => _runner.ClearHitData();
    }
}