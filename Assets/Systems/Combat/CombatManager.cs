using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using KinematicCharacterController;
using Reflex.Attributes;
using Systems.Audio;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.Data;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.Core.ResourceManagement;
using Systems.CPU;
using Systems.Input;
using Systems.Stage;
using Systems.UI.Combat;
using Systems.UI.Dev.CollisionVisualizer;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        [Inject] private readonly AudioManager _audioManager;
        [Inject] private readonly CombatUIController _uiController;

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

        public event Action<HitResult> OnHitResolved;

        private uint _firstToWinRounds = 2; // Best of 3

        #region Runtime Data

        private bool _combatInProgress;


        public float RoundTimer { get; private set; } = 99f;
        private uint _combatant0RoundsWon;
        private uint _combatant1RoundsWon;

        #endregion


        // ── Hitstop ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Remaining game ticks to freeze. Decremented at the top of LogicTick.
        /// During hitstop: combatant logic, collision, and KCC are all skipped.
        /// Camera shake and LateUpdate systems are unaffected (they run in real time).
        /// </summary>
        private uint _hitstopFramesRemaining;

        /// <summary>
        /// Freezes all gameplay logic for <paramref name="frames"/> ticks.
        /// Overlapping calls keep the longest remaining duration — a weaker hit
        /// landing during a strong hit's hitstop will never cut it short.
        /// </summary>
        public void TriggerHitstop(uint frames)
            => _hitstopFramesRemaining = (uint)Mathf.Max(_hitstopFramesRemaining, frames);

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

        public async UniTask PrepareCombat(CombatSession session,
            IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            await session.ActivateSceneAsync();

            Combatant0Behaviour = session.Combatant0;
            Combatant1Behaviour = session.Combatant1;

            var c0Provider = combatant0InputProvider;
            var c1Provider = combatant1InputProvider;

            if (combatant1InputProvider == null || combatant1InputProvider.ProviderType == EInputProviderType.Dummy)
            {
                c1Provider = new CpuInputProvider(Combatant1Behaviour, Combatant0Behaviour, session.Combatant1Data.cpuPersonality,
                    session.Combatant1Data.cpuMoveHintSheet, session.Combatant1Data.cpuDefenceHintSheet);
            }

            await _audioManager.PreloadAsync(
                Combatant0Behaviour.audioSheet.AudioEvents.Values.Concat(Combatant1Behaviour.audioSheet.AudioEvents
                    .Values));

            SetInputProviders(c0Provider, c1Provider);
        }

        public void StartCombat()
        {
            string str = "Starting combat...\n";
            str +=
                $"Combatant 0: {Combatant0Behaviour.gameObject.name} ProviderType: {Combatant0Behaviour.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            str +=
                $"Combatant 1: {Combatant1Behaviour.gameObject.name} ProviderType: {Combatant1Behaviour.InputProvider?.ProviderType.ToString() ?? "Null"}\n";
            Debug.Log(str);

            // Discard anything latched before the round (e.g. Help-pane scroll presses).
            Combatant0Behaviour.InputProvider?.Flush();
            Combatant1Behaviour.InputProvider?.Flush();

            _combatInProgress = true;

            Combatant0Behaviour.Runner.OnMoveStarted +=
                _ => _combatOverlapSolver.ClearHitRegistry(Combatant0Behaviour);
            Combatant1Behaviour.Runner.OnMoveStarted +=
                _ => _combatOverlapSolver.ClearHitRegistry(Combatant1Behaviour);

            _uiController.Show(); //<- This initializes the UI, so it must be called before any events are triggered.

            StartRound();

            OnCombatStarted?.Invoke(Combatant0Behaviour, Combatant1Behaviour);
        }

        private void EndCombat()
        {
            _combatInProgress = false;
            _uiController.Hide();
            _collisionVisualizer.Hide();

            OnCombatEnded?.Invoke();
        }

        private void StartRound()
        {
            RoundTimer = 99f;
            Combatant0Behaviour.ResetForNewRound();
            Combatant1Behaviour.ResetForNewRound();

            _uiController.ResetForNewRound();

            PositionCombatants();
        }

        private void RoundEnd()
        {
            if (_combatant0RoundsWon == _firstToWinRounds)
            {
                EndCombat();
            }
            else if (_combatant1RoundsWon == _firstToWinRounds)
            {
                EndCombat();
            }
            else
            {
                StartRound();
            }
        }

        private void RoundTimeout()
        {
            Debug.Log("Round timer expired!");

            float c0HP = Combatant0Behaviour.Stats.HPFraction;
            float c1HP = Combatant1Behaviour.Stats.HPFraction;

            if (c0HP > c1HP)
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins by timeout!");
            }
            else if (c1HP > c0HP)
            {
                _combatant1RoundsWon++;
                Debug.Log("Combatant 1 wins by timeout!");
            }
            else
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins by timeout!");
            }

            RoundEnd();
        }

        private bool CheckForCombatantDeaths()
        {
            if (Combatant0Behaviour.Stats.IsDead())
            {
                _combatant1RoundsWon++;
                Debug.Log("Combatant 1 wins the round!");

                RoundEnd();
                return true;
            }
            else if (Combatant1Behaviour.Stats.IsDead())
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins the round!");

                RoundEnd();
                return true;
            }

            return false;
        }

        public void RegisterTickable(ITickable<CombatManager> tickable)
        {
            _tickables.Add(tickable);
        }

        public void UnregisterTickable(ITickable<CombatManager> tickable)
        {
            _tickables.Remove(tickable);
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


            if (_hitstopFramesRemaining > 0)
            {
                // Skip all logic while hitstop is active.
                _hitstopFramesRemaining--;
                return;
            }

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
                        TriggerHitstop(hitData.HitstopDurationOnHit);
                        break;
                    case EHitResolution.Blocked:
                        defender.NotifyBlocked(hitResult);
                        attacker.NotifyGotBlocked(hitResult);
                        TriggerHitstop(hitData.HitstopDurationOnBlock);
                        break;
                }

                OnHitResolved?.Invoke(hitResult);
            }

            if (CheckForCombatantDeaths()) return;


            if (_hitstopFramesRemaining > 0) return; //Don't run the simulation if we triggered hitstop this frame.

            if (_kccSettings.Interpolate)
            {
                _kccSystem.PreSimulationInterpolationUpdate(TickManager.TickInterval);
            }

            _kccSystem.Simulate(TickManager.TickInterval, _kccSystem.CharacterMotors, _kccSystem.PhysicsMovers);

            if (_kccSettings.Interpolate)
            {
                _kccSystem.PostSimulationInterpolationUpdate(TickManager.TickInterval);
            }

            RoundTimer -= TickManager.TickInterval;
            _uiController.LogicTick();

            if (RoundTimer <= 0)
            {
                RoundTimeout();
            }
        }

        public void Cleanup()
        {
            Combatant0Behaviour = null;
            Combatant1Behaviour = null;

            // Reset match-level counters so the next session starts clean.
            _combatant0RoundsWon = 0;
            _combatant1RoundsWon = 0;
            _hitstopFramesRemaining = 0;
            _combatInProgress = false;
        }
    }
}