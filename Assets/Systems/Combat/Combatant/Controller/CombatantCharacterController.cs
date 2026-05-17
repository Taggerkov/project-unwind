using System;
using KinematicCharacterController;
using Systems.Combat.Combatant.Behaviour;
using UnityEngine;

namespace Systems.Combat.Combatant.Controller
{
    public class CombatantCharacterController : ICharacterController
    {
        public KinematicCharacterMotor Motor { get; set; }

        public CombatantStats Stats { get; set; }

        public event Action OnBecameAirborne;
        public event Action OnLanded;

        public int FacingSign { get; set; } = 1;

        // ── Channel 1: Constant velocity (move-driven, physics-immune) ────────────
        private Vector3 _constantVelocityCharacter;
        private Vector3 _constantVelocityWorld;

        // ── Channel 2: Free velocity (subject to gravity and friction) ────────────
        private Vector3 _freeVelocity;

        // ── Physics parameters (overridable per-move) ─────────────────────────────
        private float _gravityScale = 1f;
        private float _frictionScale = 1f;
        private bool _ignoreFriction = false;
        private bool _ignoreGravity = false;

        // ── Tunables ──────────────────────────────────────────────────────────────

        private bool _isGrounded;

        // ── Public API ─────────────────────────────────────────────────────────────

        public float FreeVelocityX => _freeVelocity.x;


        // CombatantCharacterController.cs

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

// Expose friction override for per-move custom friction (e.g. during a dash)
        public void SetFrictionScale(float scale)
        {
            _frictionScale = scale;
            _ignoreFriction = false;
        }

        public void SetConstantVelocity(Vector3 v, EVelocitySpace space = EVelocitySpace.Character)
        {
            if (space == EVelocitySpace.Character) _constantVelocityCharacter = v;
            else _constantVelocityWorld = v;
        }

        public void ClearConstantVelocity(EVelocitySpace space = EVelocitySpace.Character)
            => SetConstantVelocity(Vector3.zero, space);

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

        public void DisableGravity() => _ignoreGravity = true;
        public void DisableFriction() => _ignoreFriction = true;
        public void RestoreGravity() => _ignoreGravity = false;

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

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
        }

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

        public void BeforeCharacterUpdate(float deltaTime)
        {
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            bool isNowGrounded = Motor.GroundingStatus.IsStableOnGround;

            if (_isGrounded && !isNowGrounded)
                OnBecameAirborne?.Invoke();
            else if (!_isGrounded && isNowGrounded)
                OnLanded?.Invoke();

            _isGrounded = isNowGrounded;
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            // intentionally empty — grounding state is managed in PostGroundingUpdate
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        public void ForceUnground(float time) => Motor.ForceUnground(time);
    }
}