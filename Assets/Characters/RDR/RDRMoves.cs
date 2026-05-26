using System;
using System.Collections;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Core;
using UnityEngine;

namespace Characters.RDR
{
    /// <summary>
    /// Redeemer forward run: sustained forward movement that applies an initial burst then
    /// continuously drives X velocity. Cancels immediately on negative edge (button release).
    /// </summary>
    [Serializable]
    public class RedeemerForwardRun : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Applies the initial speed burst and registers the per-tick velocity driver.</summary>
        protected internal override void OnMoveEnter()
        {
            AddVelocity(Vector3.right * Stats.fDashInitialSpeed);
            OnEachTick(input =>
            {
                Owner.CharacterController.DriveVelocityX(Stats.fDashSpeed, Stats.fDashAcceleration,
                    TickManager.TickInterval);
            });
        }

        /// <summary>Loops pose 0 while running; registers a negative-edge cancel on entry.</summary>
        protected override IEnumerator Script()
        {
            OnNegativeEdge(Owner.Runner.Cancel);
            while (true)
            {
                yield return Pose(0, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>
    /// Redeemer back dash: brief grounded squat, then a backward arc with friction disabled
    /// until the character lands. Loops a placeholder pose while airborne.
    /// </summary>
    [Serializable]
    public class RedeemerBDash : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Set to true by the land callback; used to exit the airborne loop in Script.</summary>
        private bool _hasLanded;

        /// <summary>Registers the landing callback that signals Script to exit its airborne loop.</summary>
        protected internal override void OnMoveEnter()
        {
            _hasLanded = false;
            OnLand(() => { _hasLanded = true; });
        }

        /// <summary>Poses the squat, launches the character backward, waits for landing, then restores friction.</summary>
        protected override IEnumerator Script()
        {
            yield return Pose(999, 3);

            AddVelocity(new Vector3(-Stats.bDashSpeed, Stats.bDashJump, 0));
            BecomeAirborne();
            ForceUnground();

            DisableFriction();

            while (!_hasLanded)
            {
                yield return Pose(999, 1);
            }

            RestoreFriction();
        }
    }

    /// <summary>
    /// Redeemer air forward dash: costs one air movement action; halts momentum, briefly
    /// disables gravity, applies a forward burst, then decays velocity over a short recovery window.
    /// </summary>
    [Serializable]
    public class RedeemerAirFDash : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Blocks entry when no air movement actions remain.</summary>
        protected internal override bool CanEnter()
        {
            return Stats.CanTakeAirMovementAct;
        }

        /// <summary>Closes the kara-cancel window and consumes one air movement action.</summary>
        protected internal override void OnMoveEnter()
        {
            CloseKaraCancelWindow();
            Stats.UseAirMovementAction();
        }

        /// <summary>Halts momentum, holds position briefly, bursts forward, then decays velocity each tick through recovery.</summary>
        protected override IEnumerator Script()
        {
            HaltMomentum();
            DisableFriction();
            DisableGravity();

            yield return Pose(999, (int)Stats.airFDashTicks);

            AddVelocity(new Vector3(Stats.airFDashSpeed, 0, 0));

            yield return Pose(999, (int)Stats.airFDashBurstTicks);

            RestoreFriction();
            RestoreGravity();

            OnEachTick(_ => { ScaleFreeVelocityX(Stats.airFDashDecayFactor); });

            yield return Pose(999, 4);
        }
    }

    /// <summary>
    /// Redeemer air back dash: costs one air movement action; halts momentum, disables gravity,
    /// applies an immediate backward burst, then restores physics after the active window.
    /// </summary>
    [Serializable]
    public class RedeemerAirBDash : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Blocks entry when no air movement actions remain.</summary>
        protected internal override bool CanEnter()
        {
            return Stats.CanTakeAirMovementAct;
        }

        /// <summary>Closes the kara-cancel window and consumes one air movement action.</summary>
        protected internal override void OnMoveEnter()
        {
            CloseKaraCancelWindow();
            Stats.UseAirMovementAction();
        }

        /// <summary>Halts momentum, applies backward velocity, holds for the configured tick count, then restores physics.</summary>
        protected override IEnumerator Script()
        {
            HaltMomentum();
            DisableFriction();
            DisableGravity();

            AddVelocity(new Vector3(-Stats.airBDashSpeed, 0, 0));
            yield return Pose(999, (int)Stats.airBDashTicks);

            RestoreFriction();
            RestoreGravity();
        }
    }

    /// <summary>
    /// Redeemer standing light punch (5P): 3-frame startup, 3-frame active, 6-frame recovery.
    /// Gatlings into forward run and itself. Plays sound index 0 on hit.
    /// </summary>
    [Serializable]
    public class Redeemer5AtkP : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Registers static gatling options into forward run and self-cancel.</summary>
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerForwardRun>());
            AddStaticGatlingOption(GetMoveId<Redeemer5AtkP>());
        }

        /// <summary>Registers the on-hit sound callback.</summary>
        protected internal override void OnMoveEnter()
        {
            OnHit(() => { PlaySound(0); });
        }

        /// <summary>Startup → active (hit window) → recovery pose sequence with a light-attack hit box.</summary>
        protected override IEnumerator Script()
        {
            HitData hitData = HitData.LightAttack();

            yield return Pose(100, 3);
            yield return Pose(101, 3);
            BeginActiveState();

            using (Hit(hitData))
            {
                yield return Pose(102, 3);
            }

            BeginRecoveryState();

            yield return Pose(101, 3);
            yield return Pose(100, 3);
        }
    }

    /// <summary>
    /// Redeemer rekka chain — hit 1 of 3. Advances forward on startup; gatlings into <see cref="RedeemerRekka2"/>.
    /// </summary>
    [Serializable]
    public class RedeemerRekka1 : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Registers the static gatling option into the second rekka hit.</summary>
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerRekka2>());
        }

        /// <summary>Startup advance → active hit window → recovery; 25-frame hitstun, 8-frame blockstun.</summary>
        protected override IEnumerator Script()
        {
            AddVelocity(new Vector3(2, 0, 0));

            yield return Pose(200, 3);
            yield return Pose(201, 3);

            HitData hitData = new()
            {
                Damage = 40,
                GuardType = EGuardType.Any,
                AttackDirection = EAttackDirection.SelfToEnemy,
                HitKnockback = new Vector2(0.5f, 0),
                BlockKnockback = new Vector2(1, 0),
                BlockSelfKnockback = new Vector2(-3, 0),
                BlockstunDuration = 8,
                HitstunDuration = 25,
                HitstopDurationOnHit = 5,
                Level = EHitLevel.One
            };
            SetHitData(hitData);

            BeginActiveState();
            yield return Pose(201, 3);
            BeginRecoveryState();

            yield return Pose(201, 3);
            yield return Pose(200, 100);
        }
    }

    /// <summary>
    /// Redeemer rekka chain — hit 2 of 3. Closes the kara-cancel window; gatlings into <see cref="RedeemerRekka3"/>.
    /// </summary>
    [Serializable]
    public class RedeemerRekka2 : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Registers the static gatling option into the third rekka hit.</summary>
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerRekka3>());
        }

        /// <summary>Closes kara-cancel window, advances forward, then runs the startup → active → recovery sequence.</summary>
        protected override IEnumerator Script()
        {
            CloseKaraCancelWindow();

            AddVelocity(new Vector3(2, 0, 0));

            yield return Pose(200, 3);
            yield return Pose(201, 3);

            HitData hitData = new()
            {
                Damage = 40,
                GuardType = EGuardType.Any,
                AttackDirection = EAttackDirection.SelfToEnemy,
                HitKnockback = new Vector2(0.5f, 0),
                BlockKnockback = new Vector2(1, 0),
                BlockSelfKnockback = new Vector2(-3, 0),
                BlockstunDuration = 8,
                HitstunDuration = 25,
                HitstopDurationOnHit = 5,
                Level = EHitLevel.One
            };
            SetHitData(hitData);

            BeginActiveState();
            yield return Pose(201, 3);
            BeginRecoveryState();

            yield return Pose(201, 3);
            yield return Pose(200, 100);
        }
    }

    /// <summary>
    /// Redeemer rekka chain — hit 3 of 3. Launcher with level 5 hitstop (20 frames); closes the kara-cancel window.
    /// </summary>
    [Serializable]
    public class RedeemerRekka3 : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Closes the kara-cancel window, advances with a larger burst, then runs the startup → active → recovery sequence.</summary>
        protected override IEnumerator Script()
        {
            CloseKaraCancelWindow();

            AddVelocity(new Vector3(5, 0, 0));
            yield return Pose(200, 3);
            yield return Pose(201, 3);

            HitData hitData = new()
            {
                Damage = 40,
                GuardType = EGuardType.Any,
                AttackDirection = EAttackDirection.SelfToEnemy,
                HitKnockback = new Vector2(2f, 7.0f),
                BlockKnockback = new Vector2(1, 0),
                BlockSelfKnockback = new Vector2(-3, 0),
                IsLauncher = true,
                BlockstunDuration = 8,
                HitstunDuration = 25,
                HitstopDurationOnHit = 20,
                Level = EHitLevel.Five
            };
            SetHitData(hitData);

            BeginActiveState();
            yield return Pose(201, 3);
            BeginRecoveryState();

            yield return Pose(201, 3);
            yield return Pose(200, 3);
        }
    }
}