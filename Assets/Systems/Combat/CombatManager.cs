using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using KinematicCharacterController;
using Reflex.Attributes;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.Input;
using Systems.Stage;
using Systems.UI.Dev.CollisionVisualizer;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using _kccSystem = KinematicCharacterController.KinematicCharacterSystem;
using Object = UnityEngine.Object;


namespace Systems.Combat
{
    public enum CombatantSlot
    {
        Combatant0 = 0,
        Combatant1 = 1
    }

    public class CombatManager : ITickable<TickManager>
    {
        [Inject] private readonly TickManager _tickManager;
        [Inject] private readonly KCCSettings _kccSettings;
        [Inject] private readonly CollisionVisualizer _collisionVisualizer;

        private readonly CombatOverlapSolver _combatOverlapSolver = new();

        private List<ITickable<CombatManager>> _tickables = new();

        public CombatantBehaviour Combatant0Behaviour;
        public CombatantBehaviour Combatant1Behaviour;

        /// <summary>
        /// Event triggered when an input provider is changed for either combatant. The int parameter indicates which combatant's input provider was changed (0 or 1), and the IInputProvider parameter provides the new input provider instance.
        /// </summary>
        public event Action<CombatantSlot, IInputProvider> OnInputProviderChanged;

        public event Action<CombatantBehaviour, CombatantBehaviour> OnCombatStarted;
        public event Action OnCombatEnded;


        private bool _combatInProgress;

        private async UniTask SetCombatants(CombatantDataSO combatant0Data, CombatantDataSO combatant1Data)
        {
            var combatant0Handle = combatant0Data.combatantPrefabReference.InstantiateAsync();
            var combatant1Handle = combatant1Data.combatantPrefabReference.InstantiateAsync();

            //Wait for both combatants to finish loading
            await UniTask.WhenAll(combatant0Handle.ToUniTask(), combatant1Handle.ToUniTask());

            Combatant0Behaviour = combatant0Handle.Result.GetComponent<CombatantBehaviour>();
            Combatant1Behaviour = combatant1Handle.Result.GetComponent<CombatantBehaviour>();
        }

        private void SetInputProviders(IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            SetInputProvider(CombatantSlot.Combatant0, combatant0InputProvider);
            SetInputProvider(CombatantSlot.Combatant1, combatant1InputProvider);
        }

        public void SetInputProvider(CombatantSlot combatantSlot, IInputProvider inputProvider)
        {
            switch (combatantSlot)
            {
                case CombatantSlot.Combatant0:
                    Combatant0Behaviour.InputProvider = inputProvider;
                    OnInputProviderChanged?.Invoke(CombatantSlot.Combatant0, Combatant0Behaviour.InputProvider);
                    break;
                case CombatantSlot.Combatant1:
                    Combatant1Behaviour.InputProvider = inputProvider;
                    OnInputProviderChanged?.Invoke(CombatantSlot.Combatant1, Combatant1Behaviour.InputProvider);
                    break;
                default:
                    throw new ArgumentException("Invalid combatant index. Must be 0 or 1.");
            }
        }

        private void PositionCombatants()
        {
            var spawnMarker = Object.FindAnyObjectByType<CombatantSpawnMarker>();

            if (!spawnMarker)
            {
                throw new Exception(
                    "No CombatantSpawnMarker found in the scene. Please add one to define spawn points for combatants.");
            }

            Combatant0Behaviour.Motor.SetPositionAndRotation(spawnMarker.Combatant0SpawnPoint.position,
                spawnMarker.Combatant0SpawnPoint.rotation);
            Combatant1Behaviour.Motor.SetPositionAndRotation(spawnMarker.Combatant1SpawnPoint.position,
                spawnMarker.Combatant1SpawnPoint.rotation);
        }

        private void ResolveKnockback(ref HitResult result,
            CombatantBehaviour attacker, CombatantBehaviour defender)
        {
            var hitData = result.HitData;
            int dirSign = ResolveAttackDirectionSign(hitData, attacker, defender);

            bool isHit = result.Resolution == EHitResolution.Hit;

            var victimRaw = isHit ? hitData.HitKnockback : hitData.BlockKnockback;
            var perpetratorRaw = isHit ? hitData.HitSelfKnockback : hitData.BlockSelfKnockback;

            // Victim knockback — X axis determined by AttackDirection
            result.VictimKnockback = new Vector2(victimRaw.x * dirSign, victimRaw.y);

            // Perpetrator recoil — always Self space
            result.PerpetratorKnockback = new Vector2(
                perpetratorRaw.x * attacker.CharacterController.FacingSign,
                perpetratorRaw.y);
        }

        private int ResolveAttackDirectionSign(HitData hitData,
            CombatantBehaviour attacker, CombatantBehaviour defender)
        {
            return hitData.AttackDirection switch
            {
                EAttackDirection.Self =>
                    attacker.CharacterController.FacingSign,

                EAttackDirection.Player =>
                    defender.CharacterController.FacingSign,

                EAttackDirection.SelfToEnemy =>
                    defender.transform.position.x >= attacker.transform.position.x ? 1 : -1,

                EAttackDirection.PlayerToEnemy =>
                    attacker.transform.position.x >= defender.transform.position.x ? 1 : -1,

                _ => attacker.CharacterController.FacingSign
            };
        }

        public async UniTask PrepareCombat(SceneInstance sceneInstance, CombatantDataSO combatant0Data,
            IInputProvider combatant0InputProvider,
            CombatantDataSO combatant1Data, IInputProvider combatant1InputProvider)
        {
            await sceneInstance.ActivateAsync().ToUniTask();
            await SetCombatants(combatant0Data, combatant1Data);
            SetInputProviders(combatant0InputProvider, combatant1InputProvider);
            PositionCombatants();
        }

        public void StartCombat()
        {
            string str = "Starting combat...\n";
            str +=
                $"Combatant 0: {Combatant0Behaviour.gameObject.name} ProviderType: {Combatant0Behaviour.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            str +=
                $"Combatant 1: {Combatant1Behaviour.gameObject.name} ProviderType: {Combatant1Behaviour.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            Debug.Log(str);

            _combatInProgress = true;
            OnCombatStarted?.Invoke(Combatant0Behaviour, Combatant1Behaviour);
        }

        public void RegisterTickable(ITickable<CombatManager> tickable)
        {
            _tickables.Add(tickable);
        }

        public void RegisterHurtboxes(CombatantBehaviour combatantBehaviour, MinMaxAABB[] hurtbox)
        {
            _combatOverlapSolver.RegisterHurtboxes(combatantBehaviour, hurtbox);
            _collisionVisualizer.AddHurtboxes(hurtbox);
        }

        public void RegisterHitboxes(CombatantBehaviour combatantBehaviour, HitData hitData, MinMaxAABB[] hitbox)
        {
            _combatOverlapSolver.RegisterHitboxes(combatantBehaviour, hitData, hitbox);
            _collisionVisualizer.AddHitboxes(hitbox);
        }

        public void InputTick()
        {
            if (!_combatInProgress) return;

            // Update each unique provider exactly once, regardless of how many
            // combatants share it.
            if (Combatant0Behaviour.InputProvider == Combatant1Behaviour.InputProvider)
            {
                Combatant0Behaviour.InputProvider.UpdateFrameInput();
            }
            else
            {
                Combatant0Behaviour.InputProvider.UpdateFrameInput();
                Combatant1Behaviour.InputProvider.UpdateFrameInput();
            }

            foreach (var tickable in _tickables)
            {
                tickable.InputTick();
            }
        }

        public void LogicTick()
        {
            if (!_combatInProgress) return;

            _combatOverlapSolver.ClearFramedata();
            _collisionVisualizer.Clear();

            Combatant0Behaviour.LogicTick();
            Combatant1Behaviour.LogicTick();

            foreach (var tickable in _tickables)
            {
                tickable.LogicTick();
            }

            _collisionVisualizer.Visualize();
            var collisionList = _combatOverlapSolver.Solve();

            foreach (var incomingHits in collisionList)
            {
                var defender = incomingHits.Item1;
                var hitData = incomingHits.Item2;
                var attacker = incomingHits.Item3;

                var hitResolution = defender.NotifyIncomingHit(hitData, attacker);

                HitResult hitResult = new HitResult
                {
                    Perpetrator = attacker,
                    Victim = defender,
                    Resolution = hitResolution,
                    HitData = hitData,
                };

                // Resolve knockback direction here — only CombatManager can see both sides.
                ResolveKnockback(ref hitResult, attacker, defender);

                switch (hitResolution)
                {
                    case EHitResolution.Hit:
                        defender.NotifyGotHit(hitResult);
                        attacker.NotifyDealtHit(hitResult);
                        break;
                    case EHitResolution.Blocked:
                        defender.NotifyBlocked(hitResult);
                        attacker.NotifyGotBlocked(hitResult);
                        break;
                }
            }

            if (_kccSettings.Interpolate)
            {
                _kccSystem.PreSimulationInterpolationUpdate(TickManager.TickInterval);
            }

            _kccSystem.Simulate(TickManager.TickInterval, _kccSystem.CharacterMotors, _kccSystem.PhysicsMovers);

            if (_kccSettings.Interpolate)
            {
                _kccSystem.PostSimulationInterpolationUpdate(TickManager.TickInterval);
            }
        }
    }
}