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
    [Serializable]
    public class RedeemerCmnActStand : CombatantMove
    {
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


    [Serializable]
    public class RedeemerCmnActFWalk : CombatantMove
    {
        protected internal override void OnMoveEnter()
        {
            OnEachTick(tick =>
            {
                Owner.CharacterController.DriveVelocityX(Owner.Stats.fWalkSpeed, Owner.Stats.fWalkAcceleration, TickManager.TickInterval);
            });
        }

        protected override IEnumerator Script()
        {
            while (true)
            {
                // Debug.Log("Walking forward...");
                yield return Pose(999, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    [Serializable]
    public class RedeemerCmnActBWalk : CombatantMove
    {
        protected internal override void OnMoveEnter()
        {
            OnEachTick(input =>
            {
                Owner.CharacterController.DriveVelocityX(-Owner.Stats.bWalkSpeed, Owner.Stats.bWalkAcceleration, TickManager.TickInterval);
            });
        }

        protected override IEnumerator Script()
        {
            while (true)
            {
                // Debug.Log("Walking Backwards...");
                yield return Pose(999, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    [Serializable]
    public class RedeemerCmnActStandToCrouch : CombatantMove
    {
        protected internal override void OnMoveExit()
        {
            Debug.Log("Finished standing to crouching transition.");
        }

        protected override IEnumerator Script()
        {
            yield return Pose(301, 3);
            yield return Pose(302, 3);
        }
    }

    [Serializable]
    public class RedeemerCmnActCrouchToStand : CombatantMove
    {
        protected override IEnumerator Script()
        {
            yield return Pose(302, 3);
            yield return Pose(301, 3);
        }
    }

    [Serializable]
    public class RedeemerCmnActCrouch : CombatantMove
    {
        protected override IEnumerator Script()
        {
            while (true)
            {
                Debug.Log("Crouching neutral...");
                yield return Pose(400, 9);
                yield return Pose(401, 9);
                yield return Pose(402, 9);
                yield return Pose(401, 9);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    [Serializable]
    public class RedeemerCmnActJumpPre : CombatantMove
    {
        protected override IEnumerator Script()
        {
            SetJumpType(EntryInput.Direction.Current switch
            {
                EDirectionInput.Input7 => EJumpType.Backward,
                EDirectionInput.Input9 => EJumpType.Forward,
                _ => EJumpType.Neutral
            });

            yield return Pose(999, 3);

            // Code after the last yield runs when the pose expires, before Finish().
            // Apply impulse here so the velocity exists before KCC simulates this tick.
            // Immediately mark airborne so TryEnterMove (called same tick after Finish())
            // sees Airborne state and CmnActJump wins the candidate search.;
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

    [Serializable]
    public class RedeemerCmnActJump : CombatantMove
    {
        protected override IEnumerator Script()
        {
            while (true)
                yield return Pose(999, 1);
            // ReSharper disable once IteratorNeverReturns
        }
    }

    [Serializable]
    public class RedeemerCmnActJumpLand : CombatantMove
    {
        protected override IEnumerator Script()
        {
            yield return Pose(999, 5);
        }
    }

    [Serializable]
    public class RedeemerCmnActHitstun : CmnActHitstun
    {
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

        [Serializable]
        public class RedeemerCmnActBlockstun : CmnActBlockstun
        {
            protected override IEnumerator Script()
            {
                uint poseId = BlockPoseId();
                yield return Pose(poseId, BlockstunTicks);
            }
        }
    }
}