using System;
using System.Collections;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Controller;
using Systems.Combat.Combatant.StateMachine;
using Systems.Core;
using Systems.Input;
using UnityEngine;

namespace Characters.RDR.Common
{
    /// <summary>Redeemer standing idle: loops through a 4-pose breathing cycle indefinitely.</summary>
    [Serializable]
    public class RedeemerCmnActStand : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Cycles through poses 0–2 in a looping idle animation.</summary>
        protected override IEnumerator Script()
        {
            while (true)
            {
                yield return Pose(0, 9);
                yield return Pose(1, 9);
                yield return Pose(2, 9);
                yield return Pose(1, 9);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>Redeemer forward walk: continuously drives positive X velocity at walking speed.</summary>
    [Serializable]
    public class RedeemerCmnActFWalk : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Registers the per-tick forward velocity driver.</summary>
        protected internal override void OnMoveEnter()
        {
            OnEachTick(tick =>
            {
                Owner.CharacterController.DriveVelocityX(Owner.Stats.fWalkSpeed, Owner.Stats.fWalkAcceleration,
                    TickManager.TickInterval);
            });
        }

        /// <summary>Holds a placeholder pose each tick while walking.</summary>
        protected override IEnumerator Script()
        {
            while (true)
            {
                yield return Pose(999, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>Redeemer back walk: continuously drives negative X velocity at back-walk speed.</summary>
    [Serializable]
    public class RedeemerCmnActBWalk : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Registers the per-tick backward velocity driver.</summary>
        protected internal override void OnMoveEnter()
        {
            OnEachTick(input =>
            {
                Owner.CharacterController.DriveVelocityX(-Owner.Stats.bWalkSpeed, Owner.Stats.bWalkAcceleration,
                    TickManager.TickInterval);
            });
        }

        /// <summary>Holds a placeholder pose each tick while walking backward.</summary>
        protected override IEnumerator Script()
        {
            while (true)
            {
                yield return Pose(999, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>Redeemer standing-to-crouch transition: plays a 2-pose squat-down sequence.</summary>
    [Serializable]
    public class RedeemerCmnActStandToCrouch : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Plays the downward transition pose sequence.</summary>
        protected override IEnumerator Script()
        {
            yield return Pose(301, 3);
            yield return Pose(302, 3);
        }
    }

    /// <summary>Redeemer crouch-to-stand transition: plays the reverse 2-pose stand-up sequence.</summary>
    [Serializable]
    public class RedeemerCmnActCrouchToStand : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Plays the upward transition pose sequence.</summary>
        protected override IEnumerator Script()
        {
            yield return Pose(302, 3);
            yield return Pose(301, 3);
        }
    }

    /// <summary>Redeemer crouching idle: loops through a 4-pose crouching cycle indefinitely.</summary>
    [Serializable]
    public class RedeemerCmnActCrouch : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Cycles through poses 400–402 in a looping crouch idle animation.</summary>
        protected override IEnumerator Script()
        {
            while (true)
            {
                yield return Pose(400, 9);
                yield return Pose(401, 9);
                yield return Pose(402, 9);
                yield return Pose(401, 9);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>
    /// Redeemer pre-jump squat: applies high friction briefly, then resolves the jump direction from
    /// the entry input and launches the character with the appropriate velocity before transitioning airborne.
    /// </summary>
    [Serializable]
    public class RedeemerCmnActJumpPre : CombatantMove<RDRCombatantStats>
    {
        /// <summary>
        /// Squats for 3 ticks with high friction, resolves forward/backward/neutral jump type from
        /// input direction, applies the launch velocity, and marks the character as airborne.
        /// </summary>
        protected override IEnumerator Script()
        {
            Owner.CharacterController.SetFrictionScale(2.8f);

            yield return Pose(999, 3);

            Owner.CharacterController.RestoreFriction();

            SetJumpType(EntryInput.Direction.Current switch
            {
                EDirectionInput.Input7 => EJumpType.Backward,
                EDirectionInput.Input9 => EJumpType.Forward,
                _ => EJumpType.Neutral
            });

            AddVelocity(JumpType switch
            {
                EJumpType.Backward => new Vector3(-Stats.bJumpDistance, Stats.jumpHeight, 0),
                EJumpType.Forward => new Vector3(Stats.fJumpDistance, Stats.jumpHeight, 0),
                _ => new Vector3(0f, Stats.jumpHeight, 0)
            });

            ForceUnground();
            BecomeAirborne();
        }
    }

    /// <summary>Redeemer airborne state: loops a placeholder pose and transitions to landing on touch-down.</summary>
    [Serializable]
    public class RedeemerCmnActJump : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Cached reference to the land move resolved during initialisation.</summary>
        private CombatantMove _landMove;

        /// <summary>Resolves and caches the <see cref="RedeemerCmnActJumpLand"/> move reference.</summary>
        protected override void OnInitialize()
        {
            var id = GetMoveId<RedeemerCmnActJumpLand>();
            _landMove = Owner.GetMoveById(id);
        }

        /// <summary>Registers the land callback that cancels the jump script and starts the land move.</summary>
        protected internal override void OnMoveEnter()
        {
            OnLand(() =>
            {
                Owner.Runner.Cancel();
                Owner.StartMove(_landMove);
            });
        }

        /// <summary>Loops a placeholder pose each tick while airborne.</summary>
        protected override IEnumerator Script()
        {
            while (true)
                yield return Pose(999, 1);
            // ReSharper disable once IteratorNeverReturns
        }
    }

    /// <summary>
    /// Redeemer landing recovery: closes the kara-cancel window, resets air movement actions,
    /// enables IASA, and holds for 5 ticks.
    /// </summary>
    [Serializable]
    public class RedeemerCmnActJumpLand : CombatantMove<RDRCombatantStats>
    {
        /// <summary>Closes the kara-cancel window and resets the air movement action counter on landing.</summary>
        protected internal override void OnMoveEnter()
        {
            CloseKaraCancelWindow();
            Stats.ResetAirMovementActions();
        }

        /// <summary>Enables IASA immediately then holds a placeholder pose for the landing recovery window.</summary>
        protected override IEnumerator Script()
        {
            EnableIASA();
            yield return Pose(999, 5);
        }
    }

    /// <summary>
    /// Redeemer hitstun: plays an optional override pose or selects the appropriate damage-level pose
    /// sequence. Long hitstun (more than 4 ticks) adds brief pose transitions around the main hold.
    /// </summary>
    [Serializable]
    public class RedeemerCmnActHitstun : CmnActHitstun
    {
        /// <summary>Plays the damage pose for the duration of hitstun, with entry/exit transitions for longer stuns.</summary>
        protected override IEnumerator Script()
        {
            if (HasDamagePoseOverride)
            {
                yield return Pose(DamagePoseOverrideGlobalId, HitstunTicks);
            }

            if (HitstunTicks > 4)
            {
                yield return Pose(DamagePoseId(), 2);
                yield return Pose(DamagePoseId(1), HitstunTicks - 4);
                yield return Pose(DamagePoseId(), 2);
            }
            else
            {
                yield return Pose(DamagePoseId(1), HitstunTicks);
            }
        }

        /// <summary>Redeemer blockstun: holds the appropriate block pose for the full blockstun duration.</summary>
        [Serializable]
        public class RedeemerCmnActBlockstun : CmnActBlockstun
        {
            /// <summary>Selects the block pose by hit level and holds it for the blockstun duration.</summary>
            protected override IEnumerator Script()
            {
                uint poseId = BlockPoseId();
                yield return Pose(poseId, BlockstunTicks);
            }
        }
    }
}