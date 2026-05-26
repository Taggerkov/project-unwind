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
using Systems.UI.Overlay;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using _kccSystem = KinematicCharacterController.KinematicCharacterSystem;
using Object = UnityEngine.Object;


namespace Systems.Combat
{
    /// <summary>Which of the two combatant slots a value or event refers to.</summary>
    public enum CombatantSlot
    {
        /// <summary>The first combatant (typically player 0).</summary>
        Combatant0 = 0,

        /// <summary>The second combatant (typically player 1 or CPU).</summary>
        Combatant1 = 1
    }

    /// <summary>
    /// Orchestrates a single combat session: drives both combatants through the tick phases,
    /// runs KCC physics, resolves hitbox–hurtbox overlaps, applies hitstop, manages round and
    /// match state, and routes audio and UI events. Registered as an <see cref="ITickable{TickManager}"/>
    /// so it participates in the global 60 Hz loop.
    /// </summary>
    public class CombatManager : ITickable<TickManager>
    {
        /// <summary>Injected tick manager used to register this manager in the global loop.</summary>
        [Inject] private readonly TickManager _tickManager;

        /// <summary>KCC physics settings forwarded to the simulation each tick.</summary>
        [Inject] private readonly KCCSettings _kccSettings;

        /// <summary>Dev-only overlay that renders hitboxes and hurtboxes each frame.</summary>
        [Inject] private readonly CollisionVisualizer _collisionVisualizer;

        /// <summary>Used to preload per-character audio events before combat starts.</summary>
        [Inject] private readonly AudioManager _audioManager;

        /// <summary>In-game HUD controller shown and hidden alongside combat sessions.</summary>
        [Inject] private readonly CombatUIController _uiController;

        /// <summary>Solves hitbox–hurtbox overlaps and deduplicates per-move hits each logic tick.</summary>
        private readonly CombatOverlapSolver _combatOverlapSolver = new();

        /// <summary>Systems registered to receive CombatManager sub-ticks (e.g. dev overlays).</summary>
        private List<ITickable<CombatManager>> _tickables = new();

        /// <summary>Live combatant entity for slot 0; valid between <see cref="PrepareCombat"/> and <see cref="Cleanup"/>.</summary>
        public CombatantBehaviour Combatant0Behaviour;

        /// <summary>Live combatant entity for slot 1; valid between <see cref="PrepareCombat"/> and <see cref="Cleanup"/>.</summary>
        public CombatantBehaviour Combatant1Behaviour;

        /// <summary>
        /// Raised when either combatant's input provider is replaced. Provides the slot index
        /// and the new provider so subscribers (e.g. input history overlay) can rebind.
        /// </summary>
        public event Action<CombatantSlot, IInputProvider> OnInputProviderChanged;

        /// <summary>Raised once at the start of each round after combatants are positioned.</summary>
        public event Action<CombatantBehaviour, CombatantBehaviour> OnCombatStarted;

        /// <summary>Raised when the match ends (either player reaches <see cref="_firstToWinRounds"/>).</summary>
        public event Action OnCombatEnded;

        /// <summary>Raised once for every resolved hit or block, carrying the full result.</summary>
        public event Action<HitResult> OnHitResolved;

        /// <summary>
        /// Raised at the end of each round with the winning slot and that combatant's updated
        /// total round win count. Consumed by the UI to display a round-win indicator.
        /// </summary>
        public event Action<CombatantSlot, int> OnRoundEnded;

        /// <summary>Number of rounds a combatant must win to win the match. Defaults to best-of-3.</summary>
        private uint _firstToWinRounds = 2;

        #region Runtime Data

        /// <summary>True while a round is in progress; gates InputTick and LogicTick processing.</summary>
        private bool _combatInProgress;

        /// <summary>Seconds remaining in the current round; counts down each logic tick.</summary>
        public float RoundTimer { get; private set; } = 99f;

        /// <summary>Rounds won by combatant 0 in the current match.</summary>
        private uint _combatant0RoundsWon;

        /// <summary>Rounds won by combatant 1 in the current match.</summary>
        private uint _combatant1RoundsWon;

        /// <summary>Slot that won the most recent round; written before raising <see cref="OnRoundEnded"/>.</summary>
        private CombatantSlot _lastRoundWinner;

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

        /// <summary>Assigns input providers to both combatant slots in one call.</summary>
        private void SetInputProviders(IInputProvider combatant0InputProvider, IInputProvider combatant1InputProvider)
        {
            SetInputProvider(CombatantSlot.Combatant0, combatant0InputProvider);
            SetInputProvider(CombatantSlot.Combatant1, combatant1InputProvider);
        }

        /// <summary>
        /// Assigns a new input provider to the specified combatant slot and raises
        /// <see cref="OnInputProviderChanged"/> so subscribers can rebind.
        /// </summary>
        /// <param name="combatantSlot">Which slot to update.</param>
        /// <param name="inputProvider">The new input provider to assign.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="combatantSlot"/> is not a valid value.</exception>
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

        /// <summary>
        /// Reads the scene's <see cref="CombatantSpawnMarker"/> and teleports both combatants
        /// to their designated spawn transforms.
        /// </summary>
        /// <exception cref="Exception">Thrown when no spawn marker exists in the loaded stage scene.</exception>
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

        /// <summary>
        /// Computes world-space knockback vectors for both the victim and the perpetrator and
        /// writes them into <paramref name="result"/>. Direction is resolved by
        /// <see cref="ResolveAttackDirectionSign"/> so that hit data can be authored in
        /// character-relative terms.
        /// </summary>
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

        /// <summary>
        /// Returns +1 or −1 for the horizontal knockback direction based on the
        /// <see cref="EAttackDirection"/> mode stored in <paramref name="hitData"/>.
        /// </summary>
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

        /// <summary>
        /// Activates the stage scene, binds combatant behaviour references from the session,
        /// substitutes CPU input for any missing player provider, and preloads all character
        /// audio events before the round begins.
        /// </summary>
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
                c1Provider = new CpuInputProvider(Combatant1Behaviour, Combatant0Behaviour,
                    session.Combatant1Data.cpuPersonality,
                    session.Combatant1Data.cpuMoveHintSheet, session.Combatant1Data.cpuDefenceHintSheet);
            }

            await _audioManager.PreloadAsync(
                Combatant0Behaviour.audioSheet.AudioEvents.Values.Concat(Combatant1Behaviour.audioSheet.AudioEvents
                    .Values));

            SetInputProviders(c0Provider, c1Provider);
        }

        /// <summary>
        /// Flushes stale input, marks combat as in-progress, wires hit-registry resets,
        /// shows the UI, resets match-level UI state, starts the first round, and raises
        /// <see cref="OnCombatStarted"/>.
        /// </summary>
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
            
            _uiController.ResetForNewMatch();

            StartRound();

            OnCombatStarted?.Invoke(Combatant0Behaviour, Combatant1Behaviour);
        }

        /// <summary>Marks combat as ended, hides the HUD and collision visualizer, and raises <see cref="OnCombatEnded"/>.</summary>
        private void EndCombat()
        {
            _combatInProgress = false;
            _uiController.Hide();
            _collisionVisualizer.Hide();

            OnCombatEnded?.Invoke();
        }

        /// <summary>Resets the timer, restores both combatants to full health, resets the HUD, and positions combatants at spawn.</summary>
        private void StartRound()
        {
            RoundTimer = 99f;
            Combatant0Behaviour.ResetForNewRound();
            Combatant1Behaviour.ResetForNewRound();

            _uiController.ResetForNewRound();

            PositionCombatants();
        }

        /// <summary>
        /// Raises <see cref="OnRoundEnded"/>, then ends the match if either combatant has
        /// reached <see cref="_firstToWinRounds"/>; otherwise starts the next round.
        /// </summary>
        private void RoundEnd()
        {
            OnRoundEnded?.Invoke(_lastRoundWinner, _lastRoundWinner == CombatantSlot.Combatant0
                ? (int)_combatant0RoundsWon
                : (int)_combatant1RoundsWon);

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

        /// <summary>
        /// Awards the round to whichever combatant has a higher HP fraction when the timer
        /// expires. Ties go to combatant 0 as the arbitrary tiebreaker.
        /// </summary>
        private void RoundTimeout()
        {
            Debug.Log("Round timer expired!");

            float c0HP = Combatant0Behaviour.Stats.HPFraction;
            float c1HP = Combatant1Behaviour.Stats.HPFraction;

            if (c0HP > c1HP)
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins by timeout!");
                _lastRoundWinner = CombatantSlot.Combatant0;
            }
            else if (c1HP > c0HP)
            {
                _combatant1RoundsWon++;
                Debug.Log("Combatant 1 wins by timeout!");
                _lastRoundWinner = CombatantSlot.Combatant1;
            }
            else
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins by timeout!");
                _lastRoundWinner = CombatantSlot.Combatant0;
            }

            RoundEnd();
        }

        /// <summary>
        /// Checks whether either combatant is dead, increments the winner's round count, and
        /// calls <see cref="RoundEnd"/>. Returns <c>true</c> if a death was detected so the
        /// caller can skip the remainder of the logic tick.
        /// </summary>
        private bool CheckForCombatantDeaths()
        {
            if (Combatant0Behaviour.Stats.IsDead())
            {
                _combatant1RoundsWon++;
                Debug.Log("Combatant 1 wins the round!");
                _lastRoundWinner = CombatantSlot.Combatant1;

                RoundEnd();
                return true;
            }

            if (Combatant1Behaviour.Stats.IsDead())
            {
                _combatant0RoundsWon++;
                Debug.Log("Combatant 0 wins the round!");
                _lastRoundWinner = CombatantSlot.Combatant0;

                RoundEnd();
                return true;
            }

            return false;
        }

        /// <summary>Subscribes <paramref name="tickable"/> to receive CombatManager sub-ticks each frame.</summary>
        public void RegisterTickable(ITickable<CombatManager> tickable)
        {
            _tickables.Add(tickable);
        }

        /// <summary>Removes <paramref name="tickable"/> from the CombatManager sub-tick list.</summary>
        public void UnregisterTickable(ITickable<CombatManager> tickable)
        {
            _tickables.Remove(tickable);
        }

        /// <summary>
        /// Forwards hurtbox volumes to the overlap solver and the dev visualizer for this tick.
        /// Called by <see cref="CombatantBehaviour"/> during its logic tick.
        /// </summary>
        public void RegisterHurtboxes(CombatantBehaviour combatantBehaviour, MinMaxAABB[] hurtbox)
        {
            _combatOverlapSolver.RegisterHurtboxes(combatantBehaviour, hurtbox);
            _collisionVisualizer.AddHurtboxes(hurtbox);
        }

        /// <summary>
        /// Forwards hitbox volumes and their associated hit data to the overlap solver and the
        /// dev visualizer for this tick. Called by <see cref="CombatantBehaviour"/> during its logic tick.
        /// </summary>
        public void RegisterHitboxes(CombatantBehaviour combatantBehaviour, HitData hitData, MinMaxAABB[] hitbox)
        {
            _combatOverlapSolver.RegisterHitboxes(combatantBehaviour, hitData, hitbox);
            _collisionVisualizer.AddHitboxes(hitbox);
        }

        /// <summary>
        /// Polls each unique input provider once per tick, then forwards the tick to all
        /// registered sub-tickables. Shared providers (two combatants on the same device) are
        /// updated only once regardless of how many combatants reference them.
        /// </summary>
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

        /// <summary>
        /// Main combat simulation loop. Each tick: drains hitstop, clears per-frame collision
        /// data, advances both combatants, resolves hitbox–hurtbox overlaps, applies knockback
        /// and hitstop, runs KCC physics, ticks the timer, and checks for round-ending conditions.
        /// </summary>
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

        /// <summary>
        /// Clears all match-level state so the next session starts clean. Called by
        /// <see cref="GameManager"/> after <see cref="EndCombat"/> and asset unloading.
        /// </summary>
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