using System;
using Systems.Combat.Combatant.StateMachine;
using Systems.Combat.HitSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems.Combat.Combatant.Behaviour
{
    /// <summary>
    /// Abstract per-character data container holding both inspector-configured initial values
    /// (movement speeds, jump heights, HP) and non-serialized runtime state (current HP, pending
    /// stun context). Cloned from the <see cref="CombatantMoveSetDefinition"/> stats template at
    /// Awake so two combatants of the same type never share runtime state.
    /// </summary>
    [Serializable]
    public abstract class CombatantStats
    {
        // ── Inspector-configured initial values ───────────────────────────────────────

        /// <summary>Maximum HP for this character, used to initialise and cap <see cref="HP"/>.</summary>
        [SerializeField, Min(1), Tooltip("Maximum HP for this character.")]
        private float _maxHP = 500.0f;

        /// <summary>Top forward-walk speed in units per second.</summary>
        [SerializeField] public float fWalkSpeed = 4.5f;

        /// <summary>Acceleration applied while walking forward.</summary>
        [SerializeField] public float fWalkAcceleration = 200.0f;

        /// <summary>Top backward-walk speed in units per second.</summary>
        [SerializeField] public float bWalkSpeed = 3.5f;

        /// <summary>Acceleration applied while walking backward.</summary>
        [SerializeField] public float bWalkAcceleration = 200.0f;

        /// <summary>Sustained forward-dash speed.</summary>
        [SerializeField] public float fDashSpeed = 7.5f;

        /// <summary>Burst speed at the very first tick of a forward dash.</summary>
        [SerializeField] public float fDashInitialSpeed = 10.0f;

        /// <summary>Acceleration applied during a forward dash.</summary>
        [SerializeField] public float fDashAcceleration = 200.0f;

        /// <summary>Sustained backward-dash speed.</summary>
        [SerializeField] public float bDashSpeed = 4.50f;

        /// <summary>Upward velocity applied at the start of a backward dash.</summary>
        [SerializeField] public float bDashJump = 1.8f;

        /// <summary>Horizontal speed during an aerial forward dash.</summary>
        [SerializeField] public float airFDashSpeed = 11f;

        /// <summary>Total ticks the aerial forward dash lasts.</summary>
        [SerializeField] public uint airFDashTicks = 12;

        /// <summary>Ticks during which the dash maintains its burst speed before decaying.</summary>
        [SerializeField] public uint airFDashBurstTicks = 4;

        /// <summary>Per-tick velocity multiplier applied after the burst window of an aerial forward dash.</summary>
        [SerializeField] public float airFDashDecayFactor = 0.9f;

        /// <summary>Horizontal speed during an aerial backward dash.</summary>
        [SerializeField] public float airBDashSpeed = 5f;

        /// <summary>Total ticks the aerial backward dash lasts.</summary>
        [SerializeField] public uint airBDashTicks = 10;

        /// <summary>Jump apex height in units.</summary>
        [SerializeField] public float jumpHeight = 6.5f;

        /// <summary>Horizontal distance covered by a forward jump.</summary>
        [SerializeField] public float fJumpDistance = 4.65f;

        /// <summary>Horizontal distance covered by a backward jump.</summary>
        [SerializeField] public float bJumpDistance = 3.5f;

        /// <summary>Gravitational acceleration in units per second squared.</summary>
        [SerializeField] public float gravity = 9.81f;

        /// <summary>Friction coefficient applied to horizontal velocity while grounded.</summary>
        [SerializeField] public float groundFriction = 35f;

        /// <summary>Friction coefficient applied to horizontal velocity while airborne.</summary>
        [SerializeField] public float airFriction = 2f;

        // ── Runtime state (NonSerialized — never shared via the ScriptableObject) ─────

        /// <summary>Current hit points. Modified by combat; never persisted to the asset.</summary>
        [NonSerialized] public float HP;

        /// <summary>Ceiling copied from <c>_maxHP</c> on <see cref="Initialize"/>.</summary>
        [NonSerialized] public float MaxHP;

        

        // ── Pending stun context (written by CombatantBehaviour on hit/block) ─────────

        /// <summary>Hitstun ticks remaining for the current/incoming hit. Read by CmnActHitstun.</summary>
        [NonSerialized] public uint PendingHitstunTicks;

        /// <summary>Blockstun ticks remaining for the current/incoming hit. Read by CmnActBlockstun.</summary>
        [NonSerialized] public uint PendingBlockstunTicks;

        /// <summary>Hit level of the attack that triggered the current stun. Drives pose selection.</summary>
        [NonSerialized] public EHitLevel PendingHitLevel;

        /// <summary>When true, the attacker specified a custom damage pose (e.g. a grab).</summary>
        [NonSerialized] public bool PendingDamagePoseOverride;

        /// <summary>Global pose ID of the custom damage pose. Valid only when PendingDamagePoseOverride is true.</summary>
        [NonSerialized] public uint PendingDamagePoseOverrideId;

        /// <summary>Directional intent of the next jump, set by CmnActJumpPre and consumed by CmnActJump.</summary>
        [NonSerialized] public EJumpType PendingJumpType;

        // ── Lifecycle ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Copies inspector values into runtime fields and resets all per-round counters.
        /// Always call <c>base.Initialize()</c> first when overriding.
        /// </summary>
        public virtual void Initialize()
        {
            MaxHP = _maxHP;
            HP = MaxHP;
        }

        /// <summary>
        /// Produces a shallow copy of this template so each combatant instance gets
        /// independent runtime state. MemberwiseClone() is sufficient for the common
        /// case (value-type fields only); override if your subclass holds reference-type
        /// runtime state that must be deep-copied.
        /// </summary>
        public abstract CombatantStats Clone();

        // ── Helpers ───────────────────────────────────────────────────────────────────

        /// <summary>True when the character has no HP remaining.</summary>
        public bool ShouldDie => HP <= 0;

        /// <summary>HP as a 0–1 fraction, clamped.</summary>
        public float HPFraction => MaxHP > 0 ? Mathf.Clamp01((float)HP / MaxHP) : 0f;

        /// <summary>
        /// Applies damage, clamping HP to zero. Returns the actual damage dealt
        /// (may be less than <paramref name="amount"/> if the character was near death).
        /// </summary>
        public float ApplyDamage(float amount)
        {
            float before = HP;
            HP = Mathf.Max(0, HP - amount);
            return before - HP;
        }

        /// <summary>
        /// Returns true when the character should be considered dead. Override to add
        /// character-specific survival conditions (e.g. last-stand mechanics).
        /// </summary>
        public virtual bool IsDead()
        {
            return ShouldDie;
        }
    }
}