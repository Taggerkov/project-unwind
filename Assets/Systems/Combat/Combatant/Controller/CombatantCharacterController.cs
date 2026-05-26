using System;
using KinematicCharacterController;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;

namespace Systems.Combat.Combatant.Controller
{
    /// <summary>
    /// KCC <see cref="ICharacterController"/> implementation for fighting game characters.
    /// Manages two velocity channels: a move-driven constant channel (physics-immune) and a
    /// free channel subject to gravity and friction. Per-move physics overrides (gravity scale,
    /// friction scale, disable flags) are always reset by <see cref="ResetPhysicsOverrides"/>
    /// on move exit so they never leak between moves.
    /// </summary>
    public class CombatantCharacterController : ICharacterController
    {
        /// <summary>The KCC motor this controller drives; set by <see cref="CombatantBehaviour"/> at Awake.</summary>
        public KinematicCharacterMotor Motor { get; set; }

        /// <summary>Reference to the combatant's cloned stats for per-character gravity and friction values.</summary>
        public CombatantStats Stats { get; set; }

        /// <summary>Raised by <see cref="PostGroundingUpdate"/> when the character transitions from grounded to airborne.</summary>
        public event Action OnBecameAirborne;

        /// <summary>Raised by <see cref="PostGroundingUpdate"/> when the character transitions from airborne to grounded.</summary>
        public event Action OnLanded;

        /// <summary>+1 when facing right, −1 when facing left. Used to convert character-space velocity to world-space X.</summary>
        public int FacingSign { get; set; } = 1;

        // ── Channel 1: Constant velocity (move-driven, physics-immune) ────────────
        /// <summary>Constant velocity in character space (X is flipped by FacingSign before compositing).</summary>
        private Vector3 _constantVelocityCharacter;

        /// <summary>Constant velocity already in world space; added directly without facing-sign adjustment.</summary>
        private Vector3 _constantVelocityWorld;

        // ── Channel 2: Free velocity (subject to gravity and friction) ────────────
        /// <summary>Free velocity accumulator, affected by gravity, friction, and impulse calls.</summary>
        private Vector3 _freeVelocity;

        // ── Physics parameters (overridable per-move) ─────────────────────────────
        /// <summary>Multiplier applied to stats gravity each tick; reset to 1 by <see cref="ResetPhysicsOverrides"/>.</summary>
        private float _gravityScale = 1f;

        /// <summary>Multiplier applied to stats friction each tick; reset to 1 by <see cref="ResetPhysicsOverrides"/>.</summary>
        private float _frictionScale = 1f;

        /// <summary>When true, friction is not applied this tick regardless of grounding state.</summary>
        private bool _ignoreFriction = false;

        /// <summary>When true, gravity is not applied this tick.</summary>
        private bool _ignoreGravity = false;

        /// <summary>Cached grounding state from the previous <see cref="PostGroundingUpdate"/>.</summary>
        private bool _isGrounded;

        /// <summary>Horizontal component of the free velocity channel, read by move scripts.</summary>
        public float FreeVelocityX => _freeVelocity.x;

        /// <summary>
        /// Drives freeVelocity.x toward <paramref name="targetCharacterSpeed"/> by at most
        /// <paramref name="acceleration"/> per second.
        /// Like AddVelocity, the target is in character space: positive = forward (toward opponent),
        /// and is flipped automatically when the character faces left.
        /// </summary>
        public void DriveVelocityX(float targetCharacterSpeed, float acceleration, float deltaTime)
        {
            float targetWorld = targetCharacterSpeed * FacingSign;
            float delta = targetWorld - _freeVelocity.x;
            float step = Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), acceleration * deltaTime);
            _freeVelocity.x += step;
        }

        /// <summary>Clamps |freeVelocity.x| to maxSpeed, preserving direction.</summary>
        public void ClampFreeVelocityX(float maxSpeed)
        {
            _freeVelocity.x = Mathf.Clamp(_freeVelocity.x, -maxSpeed, maxSpeed);
        }
        
        /// <summary>
        /// Multiplies freeVelocity.x by <paramref name="factor"/> in place.
        /// Use for exponential velocity decay: e.g. ScaleFreeVelocityX(0.9f) each tick
        /// mirrors ArcSys' velocityXPercentEachFrame: 90.
        /// </summary>
        public void ScaleFreeVelocityX(float factor)
        {
            _freeVelocity.x *= factor;
        }

        /// <summary>Sets the friction multiplier and re-enables friction if it had been disabled.</summary>
        public void SetFrictionScale(float scale)
        {
            _frictionScale = scale;
            _ignoreFriction = false;
        }

        /// <summary>Sets the constant velocity channel for the given <paramref name="space"/>; overwrites the previous value.</summary>
        public void SetConstantVelocity(Vector3 v, EVelocitySpace space = EVelocitySpace.Character)
        {
            if (space == EVelocitySpace.Character) _constantVelocityCharacter = v;
            else _constantVelocityWorld = v;
        }

        /// <summary>Zeroes the constant velocity channel for the given <paramref name="space"/>.</summary>
        public void ClearConstantVelocity(EVelocitySpace space = EVelocitySpace.Character)
            => SetConstantVelocity(Vector3.zero, space);

        /// <summary>Zeroes both constant velocity channels (character and world space).</summary>
        public void ClearAllConstantVelocity()
        {
            _constantVelocityCharacter = Vector3.zero;
            _constantVelocityWorld = Vector3.zero;
        }

        /// <summary>One-shot impulse into the free velocity channel.</summary>
        public void AddVelocity(Vector3 v, EVelocitySpace space = EVelocitySpace.Character)
        {
            if (space == EVelocitySpace.Character) v.x *= FacingSign;
            _freeVelocity += v;
        }

        /// <summary>Zeros free velocity. Equivalent to ArcSys haltMomentum.</summary>
        public void HaltMomentum() => _freeVelocity = Vector3.zero;

        /// <summary>Overrides gravity multiplier for this move. 0 = floaty, negative = reverse.</summary>
        public void SetGravityScale(float scale)
        {
            _gravityScale = scale;
            _ignoreGravity = false;
        }

        /// <summary>Suppresses gravity for the current tick until <see cref="RestoreGravity"/> or <see cref="ResetPhysicsOverrides"/> is called.</summary>
        public void DisableGravity() => _ignoreGravity = true;

        /// <summary>Suppresses friction for the current tick until <see cref="RestoreFriction"/> or <see cref="ResetPhysicsOverrides"/> is called.</summary>
        public void DisableFriction() => _ignoreFriction = true;

        /// <summary>Re-enables gravity without resetting the gravity scale.</summary>
        public void RestoreGravity() => _ignoreGravity = false;

        /// <summary>Re-enables friction and resets the friction scale to 1.</summary>
        public void RestoreFriction()
        {
            _ignoreFriction = false;
            _frictionScale = 1f;
        }

        /// <summary>
        /// Resets all per-move overrides back to defaults.
        /// Called by MoveRunner on move exit so overrides never leak between moves.
        /// </summary>
        public void ResetPhysicsOverrides()
        {
            _gravityScale = 1f;
            _frictionScale = 1f;
            _ignoreFriction = false;
            _ignoreGravity = false;
        }

        // ── KCC integration ────────────────────────────────────────────────────────

        /// <summary>Not used — rotation is managed directly by <see cref="CombatantBehaviour.SetFacingDirection"/>.</summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
        }

        /// <summary>
        /// Called by KCC each simulation step. Applies gravity and friction to the free velocity
        /// channel, clamps vertical free velocity at the floor, then composes all channels into
        /// <paramref name="currentVelocity"/>.
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // ── Gravity ───────────────────────────────────────────────────────────
            if (!_ignoreGravity && !_isGrounded)
                _freeVelocity.y -= Stats.gravity * _gravityScale * deltaTime;

            // ── Friction (horizontal only, applied to free velocity) ──────────────
            if (!_ignoreFriction)
            {
                float friction = (_isGrounded ? Stats.groundFriction : Stats.airFriction) * _frictionScale;
                float sign = Mathf.Sign(_freeVelocity.x);
                float reduced = Mathf.Abs(_freeVelocity.x) - friction * deltaTime;
                _freeVelocity.x = reduced <= 0f ? 0f : reduced * sign;
            }

            // ── Clamp vertical free velocity at floor ─────────────────────────────
            if (_isGrounded && _freeVelocity.y < 0f)
                _freeVelocity.y = 0f;

            // ── Compose final velocity ─────────────────────────────────────────────
            var charConstant = _constantVelocityCharacter;
            charConstant.x *= FacingSign;

            currentVelocity = charConstant + _constantVelocityWorld + _freeVelocity;
        }

        /// <summary>KCC callback before the character update step; not used.</summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// Detects grounding state transitions and raises <see cref="OnBecameAirborne"/> or
        /// <see cref="OnLanded"/> accordingly so the state machine can react.
        /// </summary>
        public void PostGroundingUpdate(float deltaTime)
        {
            bool isNowGrounded = Motor.GroundingStatus.IsStableOnGround;

            if (_isGrounded && !isNowGrounded)
                OnBecameAirborne?.Invoke();
            else if (!_isGrounded && isNowGrounded)
                OnLanded?.Invoke();

            _isGrounded = isNowGrounded;
        }

        /// <summary>KCC callback after the character update step; not used.</summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
        }

        /// <summary>All colliders are valid; hit-system filtering is handled by the overlap solver, not KCC.</summary>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        /// <summary>Not used; grounding state is managed in <see cref="PostGroundingUpdate"/>.</summary>
        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>KCC movement-hit callback; not used.</summary>
        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>KCC hit-stability callback; not used.</summary>
        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>KCC discrete-collision callback; not used.</summary>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        /// <summary>Delegates to the KCC motor to force the character off the ground for <paramref name="time"/> seconds.</summary>
        public void ForceUnground(float time) => Motor.ForceUnground(time);
    }
}