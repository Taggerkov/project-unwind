using System;
using System.Collections;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.HitSystem;
using Systems.Core;
using UnityEngine;

namespace Characters.RDR
{
    [Serializable]
    public class RedeemerForwardRun : CombatantMove<RDRCombatantStats>
    {
        // public override bool IsRegistered { get; } = false;

        protected internal override void OnMoveEnter()
        {
            AddVelocity(Vector3.right * Stats.fDashInitialSpeed);
            OnEachTick(input =>
            {
                Owner.CharacterController.DriveVelocityX(Stats.fDashSpeed, Stats.fDashAcceleration,
                    TickManager.TickInterval);
            });
        }

        protected override IEnumerator Script()
        {
            OnNegativeEdge(Owner.Runner.Cancel);
            while (true)
            {
                Debug.Log("I'm running! Woooosh!");
                yield return Pose(0, 1);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }

    [Serializable]
    public class RedeemerBDash : CombatantMove<RDRCombatantStats>
    {
        private bool _hasLanded;


        protected internal override void OnMoveEnter()
        {
            _hasLanded = false;
            OnLand(() => { _hasLanded = true; });
        }

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

            Debug.Log("Landed from backdash!");

            RestoreFriction();
        }
    }

    [Serializable]
    public class RedeemerAirFDash : CombatantMove<RDRCombatantStats>
    {
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
            
            OnEachTick(_ =>
            {
                ScaleFreeVelocityX(Stats.airFDashDecayFactor);
            });
            
            yield return Pose(999, 4);
        }
    }

    [Serializable]
    public class RedeemerAirBDash : CombatantMove<RDRCombatantStats>
    {
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

    [Serializable]
    public class Redeemer5AtkP : CombatantMove<RDRCombatantStats>
    {
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerForwardRun>());
            AddStaticGatlingOption(GetMoveId<Redeemer5AtkP>());
        }

        protected internal override void OnMoveEnter()
        {
            OnHit(() => { PlaySound(0); });
        }

        protected override IEnumerator Script()
        {
            HitData hitData = HitData.LightAttack();

            Debug.Log("Starting 5P...");
            yield return Pose(100, 3);
            yield return Pose(101, 3);
            BeginActiveState();
            Debug.Log("Hitting with 5P...");

            using (Hit(hitData))
            {
                yield return Pose(102, 3);
            }

            BeginRecoveryState();
            Debug.Log("Recovering from 5P...");

            yield return Pose(101, 3);
            yield return Pose(100, 3);
        }
    }

    [Serializable]
    public class RedeemerRekka1 : CombatantMove<RDRCombatantStats>
    {
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerRekka2>());
        }

        protected override IEnumerator Script()
        {
            Debug.Log("Starting Rekka 1...");

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
            Debug.Log("Hitting with Rekka 1...");
            yield return Pose(201, 3);
            BeginRecoveryState();
            Debug.Log("Recovering from Rekka 1...");

            yield return Pose(201, 3);
            yield return Pose(200, 100);
        }
    }

    [Serializable]
    public class RedeemerRekka2 : CombatantMove<RDRCombatantStats>
    {
        protected override void OnInitialize()
        {
            AddStaticGatlingOption(GetMoveId<RedeemerRekka3>());
        }

        protected override IEnumerator Script()
        {
            CloseKaraCancelWindow();

            AddVelocity(new Vector3(2, 0, 0));

            Debug.Log("Starting Rekka 2...");
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
            Debug.Log("Hitting with Rekka 2...");
            yield return Pose(201, 3);
            BeginRecoveryState();
            Debug.Log("Recovering from Rekka 2...");

            yield return Pose(201, 3);
            yield return Pose(200, 100);
        }
    }

    [Serializable]
    public class RedeemerRekka3 : CombatantMove<RDRCombatantStats>
    {
        protected override IEnumerator Script()
        {
            CloseKaraCancelWindow();

            AddVelocity(new Vector3(5, 0, 0));

            Debug.Log("Starting Rekka 3...");
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
            Debug.Log("Hitting with Rekka 3...");
            yield return Pose(201, 3);
            BeginRecoveryState();
            Debug.Log("Recovering from Rekka 3...");

            yield return Pose(201, 3);
            yield return Pose(200, 3);
        }
    }
}