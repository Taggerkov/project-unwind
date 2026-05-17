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
        Self         = 0, // attacker's facing defines forward
        Player       = 1, // defender's facing defines forward
        SelfToEnemy  = 2, // physical direction attacker → defender
        PlayerToEnemy= 3  // physical direction defender → attacker
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
        public EHitLevel Level; //The level of this hit, used for scaling damage and hitstun/blockstun aswell as as clash resolution.
        public float Damage; // The amount of damage this hit will deal if it connects before scaling.
        public EGuardType GuardType; // The type of guard required to block this hit. If the victim's guard type matches this, the hit will be blocked instead of hitting.
        public EHitTarget HitTarget; // The type of target this hit can affect from the point of view of the perpetrator.
        public uint HitstunDuration; // The amount of hitstun (in ticks) that the victim will suffer if the hit connects and is not blocked.
        public uint BlockstunDuration; // The amount of blockstun (in ticks) that the victim will suffer if the hit is blocked.
        
        public EAttackDirection AttackDirection; // how HitKnockback/BlockKnockback X is interpreted
        
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
}