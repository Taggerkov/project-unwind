using System;
using Systems.Combat.Combatant.Behaviour;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace Systems.Combat.HitSystem
{
    /// <summary>Intensity tier of a hit, used for damage scaling, hitstun/blockstun selection, and damage pose selection.</summary>
    public enum EHitLevel
    {
        /// <summary>Lightest hit tier.</summary>
        One,

        /// <summary>Medium-light hit tier.</summary>
        Two,

        /// <summary>Medium hit tier.</summary>
        Three,

        /// <summary>Medium-heavy hit tier.</summary>
        Four,

        /// <summary>Heaviest hit tier.</summary>
        Five
    }

    /// <summary>Controls how the horizontal knockback direction is determined, allowing moves to be authored independent of facing.</summary>
    public enum EAttackDirection
    {
        /// <summary>X knockback follows the attacker's facing sign.</summary>
        Self = 0,

        /// <summary>X knockback follows the defender's facing sign.</summary>
        Player = 1,

        /// <summary>X knockback pushes in the physical direction from attacker to defender.</summary>
        SelfToEnemy = 2,

        /// <summary>X knockback pushes in the physical direction from defender to attacker.</summary>
        PlayerToEnemy = 3
    }

    /// <summary>Outcome of a resolved hitbox–hurtbox overlap, determined by the defender's blocking state.</summary>
    public enum EHitResolution
    {
        /// <summary>The hit landed and dealt damage and hitstun.</summary>
        Hit,

        /// <summary>The hit was blocked; blockstun and reduced knockback applied instead.</summary>
        Blocked,

        /// <summary>The hit was negated by armor; no stun or damage applied.</summary>
        Armored
    }

    /// <summary>Restricts which combatants a set of hitboxes can interact with.</summary>
    public enum EHitTarget
    {
        /// <summary>Only targets combatants considered enemies of the attacker.</summary>
        Enemy,

        /// <summary>Only targets combatants considered allies of the attacker.</summary>
        Ally,

        /// <summary>Targets any combatant regardless of team.</summary>
        Any
    }

    /// <summary>
    /// All parameters for a single attack interaction: damage, timing (hitstun, blockstun, hitstop),
    /// knockback vectors (on hit and on block for both sides), hit level, guard requirements, and
    /// the damage pose override system. Set by move scripts during the Active phase.
    /// </summary>
    public struct HitData
    {
        /// <summary>Unique ID assigned by the DSL. Used to prevent the same hit from being processed more than once across frames.</summary>
        public uint HitId;

        /// <summary>Hit tier; drives damage pose selection and is used for clash resolution.</summary>
        public EHitLevel Level;

        /// <summary>Raw damage dealt to the victim on hit before any scaling.</summary>
        public float Damage;

        /// <summary>Guard type the victim must hold to block this attack.</summary>
        public EGuardType GuardType;

        /// <summary>Which combatants (enemy, ally, any) this hit's boxes interact with.</summary>
        public EHitTarget HitTarget;

        /// <summary>Frames of hitstun applied to the victim when the hit connects unblocked.</summary>
        public uint HitstunDuration;

        /// <summary>Frames of blockstun applied to the victim when the hit is blocked.</summary>
        public uint BlockstunDuration;

        /// <summary>Frames of global hitstop (gameplay freeze) triggered when the hit is blocked.</summary>
        public uint HitstopDurationOnBlock;

        /// <summary>Frames of global hitstop triggered when the hit connects.</summary>
        public uint HitstopDurationOnHit;

        /// <summary>Determines how the horizontal knockback X component maps to world space.</summary>
        public EAttackDirection AttackDirection;

        /// <summary>When true, the hit briefly forces the victim airborne, enabling aerial combos.</summary>
        public bool IsLauncher;

        /// <summary>Knockback vector (world-space X resolved at runtime) applied to the victim on hit.</summary>
        public Vector2 HitKnockback;

        /// <summary>Knockback vector applied to the attacker on hit (recoil).</summary>
        public Vector2 HitSelfKnockback;

        /// <summary>Knockback vector applied to the victim when the hit is blocked.</summary>
        public Vector2 BlockKnockback;

        /// <summary>Knockback vector applied to the attacker when the hit is blocked (pushback).</summary>
        public Vector2 BlockSelfKnockback;

        /// <summary>
        /// When true, the victim is forced into DamagePoseOverrideId instead of the
        /// level-based damage pose. Use for grabs, cinematic supers, etc.
        /// Mirrors ArcSys's damageSprite override.
        /// </summary>
        public bool OverrideDamagePose;

        /// <summary>Global pose ID (collectionId * 100 + poseId) to use when OverrideDamagePose is true.</summary>
        public uint DamagePoseOverrideId;

        /// <summary>How much the combo counter increments when this hit connects.</summary>
        public uint ComboCounterIncrease;

        /// <summary>Returns a <see cref="HitData"/> pre-configured for a light attack (level 1, 20 damage, 9 hitstun).</summary>
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

        /// <summary>Returns a <see cref="HitData"/> pre-configured for a medium attack (level 2, 35 damage, 16 hitstun).</summary>
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

        /// <summary>Returns a <see cref="HitData"/> pre-configured for a heavy attack (level 3, 50 damage, 25 hitstun).</summary>
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

    /// <summary>
    /// Result of a resolved hitbox–hurtbox collision, bundling both combatants, the outcome,
    /// the originating hit data, and world-space knockback vectors pre-computed by
    /// <see cref="CombatManager"/>.
    /// </summary>
    public struct HitResult
    {
        /// <summary>The combatant whose hitbox triggered the collision.</summary>
        public CombatantBehaviour Perpetrator;

        /// <summary>The combatant whose hurtbox was overlapped.</summary>
        public CombatantBehaviour Victim;

        /// <summary>Whether the attack hit, was blocked, or was negated by armor.</summary>
        public EHitResolution Resolution;

        /// <summary>The original hit data from the attacker's move script.</summary>
        public HitData HitData;

        /// <summary>World-space knockback applied to the victim, pre-computed by <see cref="CombatManager.ResolveKnockback"/>.</summary>
        public Vector2 VictimKnockback;

        /// <summary>World-space recoil applied to the attacker, pre-computed by <see cref="CombatManager.ResolveKnockback"/>.</summary>
        public Vector2 PerpetratorKnockback;
    }

    /// <summary>
    /// Disposable handle returned by <see cref="Hit"/>. Clears active hit data when
    /// the using block exits so stale data never leaks into the next pose.
    /// </summary>
    /// <summary>
    /// Disposable handle returned by the <c>Hit()</c> DSL method. Clears the active hit data
    /// from the runner when the <c>using</c> block exits so stale data never leaks into the next pose.
    /// </summary>
    public sealed class HitScope : IDisposable
    {
        private readonly MoveRunner _runner;

        /// <summary>Creates a scope tied to <paramref name="runner"/>; call <see cref="Dispose"/> (via <c>using</c>) to clear hit data.</summary>
        internal HitScope(MoveRunner runner) => _runner = runner;

        /// <summary>Clears the runner's active hit data.</summary>
        public void Dispose() => _runner.ClearHitData();
    }
}