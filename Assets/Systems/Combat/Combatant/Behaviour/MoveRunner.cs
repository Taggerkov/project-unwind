using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Systems.Combat.HitSystem;
using Systems.Core;
using Systems.Input;
using UnityEngine;

namespace Systems.Combat.Combatant.Behaviour
{
    /// <summary>
    /// Drives a CombatantMove's Script() coroutine tick by tick.
    ///
    /// Lifecycle:
    ///   Start()  → runs the IMMEDIATE block (everything before the first Pose yield).
    ///   Tick()   → called every combat tick; decrements pose timer, advances when it hits 0.
    ///   Cancel() → interrupts from outside (cancel, hitstun, etc.).
    ///
    /// IsRunning is false until Start() is called, and becomes false again when the script
    /// exhausts naturally or Cancel() is called. CombatantBehaviour checks this to decide
    /// whether to tick the runner or try to enter a new move.
    /// </summary>
    public class MoveRunner : ITickable<CombatantBehaviour>
    {
        private const int
            KaraCancelWindow =
                3; // Ticks a move allows to be canceled at start-up. Needed to allow leniancy for multiple-button inputs that can't be perfectly simultaneous.

        private CombatantMove _currentMove;
        private IEnumerator _script;
        private PoseYield _currentPose;
        private int _ticksRemaining;

        private uint _hitIdCounter;

        private EButtonInput _entryButton;
        private EDirectionInput _entryDirection;

        private int _ticksRun;

        /// <summary>
        /// Character-space input frame captured at the moment the move was entered.
        /// Available from the IMMEDIATE block onward; safe to read in Script().
        /// </summary>
        public TickInput EntryInput { get; private set; }

        /// <summary>
        /// Character-space input frame for the tick currently being processed.
        /// Updated at the top of every LogicTick call; mirrors what OnEachTick handlers receive.
        /// </summary>
        public TickInput CurrentInput { get; private set; }

        /// <summary>True while a move is actively executing.</summary>
        public bool IsRunning { get; private set; }

        private CombatantBehaviour _owner;

        public CombatantMove CurrentMove => _currentMove;

        public event Action<CombatantMove> OnMoveStarted;
        public event Action<CombatantMove> OnMoveFinished;

        /// <summary>
        /// Fires when the move's current pose changes.
        /// Parameters are the pose's global id, collection id, and pose id within the collection, respectively.
        /// </summary>
        public event Action<uint, uint, uint> OnPoseChanged;

        // After:
        /// <summary>
        /// True from the moment this move lands a hit or is blocked, until the move ends.
        /// Used to open the gatling cancel window. Intentionally not reset per-tick —
        /// once confirmed, the window stays open through recovery.
        /// When hitstop is implemented, this pairs naturally: the freeze happens on the
        /// same tick the flag is set, giving the player time to input during the stop.
        /// </summary>
        public bool HitConfirmed { get; private set; }

        public uint NextHitId() => ++_hitIdCounter;

        public void ClearHitData() => _owner.StateMachine.SetHitData(default);

        public void Initialize(CombatantBehaviour owner)
        {
            _owner = owner;
        }

        public void ResetForNewRound()
        {
            _currentMove = null;
            _script = null;
            _currentPose = null;
            IsRunning = false;
            HitConfirmed = false;
            _ticksRun = 0;
            _entryButton = EButtonInput.None;
            _entryDirection = EDirectionInput.None;
            EntryInput = default;
            CurrentInput = default;
        }

        // ── Start ──────────────────────────────────────────────────────────────────────

        public void Start(CombatantMove move, MoveMatchResult matchResult, TickInput entryInput)
        {
            _currentMove = move;
            _script = move.GetScript();
            IsRunning = true;
            HitConfirmed = false;
            _ticksRun = 0;
            _entryButton = matchResult.TriggerButton;
            _entryDirection = matchResult.TriggerDirection;
            EntryInput = entryInput;
            CurrentInput = entryInput;

            _currentMove.ClearDynamicMoveState();

            _owner.StateMachine.ResetMoveExecutionState();

            move.OnMoveEnter();
            OnMoveStarted?.Invoke(move);

            // Runs everything before the first yield return Pose() — the IMMEDIATE block.
            Advance();
        }

        // ── Per-Tick ───────────────────────────────────────────────────────────────────

        public void LogicTick(TickInput input)
        {
            if (!IsRunning) return;

            // Keep CurrentInput in sync so Script() always sees the live character-space frame.
            CurrentInput = input;

            _ticksRun++;

            if (!_owner.StateMachine.IsKaraCancelOverriden)
            {
                if (_ticksRun >= KaraCancelWindow)
                {
                    CloseKaraCancelWindow();
                }
            }

            foreach (var handler in _currentMove.OnTickHandlers)
                handler(input);

            // The move may have cancelled itself inside an OnTick handler, so check before proceeding.
            if (_script == null) return;

            _ticksRemaining--;
            if (_ticksRemaining <= 0)
                Advance();
        }

        // ── Advance ────────────────────────────────────────────────────────────────────

        private void Advance()
        {
            if (_script.MoveNext())
            {
                _currentPose = (PoseYield)_script.Current;
                _ticksRemaining = _currentPose.Ticks;

                //Find what collection the pose belongs to via it's global id.
                //Ids go from 0-99 for each collection, so collection 1 has ids 100-199, collection 2 has 200-299, etc.
                OnPoseChanged?.Invoke(_currentPose.Id, _currentPose.CollectionId, _currentPose.PoseId);
            }
            else
            {
                Finish();
            }
        }

        // ── Event notifications ────────────────────────────────────────────────────────

        public void NotifyDealtHit()
        {
            HitConfirmed = true;
            foreach (var h in _currentMove.OnHitHandlers) h();
        }

        public void NotifyGotHit()
        {
        }

        public void NotifyBlocked()
        {
        }

        public void NotifyGotBlocked()
        {
            HitConfirmed = true;
            foreach (var h in _currentMove.OnGuardHandlers) h();
        }

        public void NotifyLand()
        {
            foreach (var h in _currentMove.OnLandHandlers) h();
        }

        // ── Cancel queries ─────────────────────────────────────────────────────────────

        public bool CanGatlingInto(uint moveId)
            => IsRunning && CurrentMove.GetGatlingOptions().Contains(moveId);

        public bool CanWhiffCancelInto(uint moveId)
            => IsRunning && CurrentMove.GetWhiffCancelOptions().Contains(moveId);

        public bool CanKaraCancel() => IsRunning && _owner.StateMachine.IsKaraCancelWindowOpen;

        [CanBeNull]
        public List<EMoveType> GetAllowedCancelCategories(EMoveType activeType)
        {
            return activeType switch
            {
                EMoveType.Movement => new List<EMoveType> { EMoveType.Normal, EMoveType.Special, EMoveType.Overdrive },
                EMoveType.Normal => new List<EMoveType> { EMoveType.Special, EMoveType.Overdrive },
                EMoveType.Special => new List<EMoveType> { EMoveType.Overdrive },
                _ => null
            };
        }

        public void RegisterNegativeEdge(Action handler)
        {
            var button = _entryButton;
            var direction = _entryDirection;

            _currentMove.OnTickHandlers.Add(input =>
            {
                bool buttonReleased = button != EButtonInput.None && GetButtonState(input, button).Released;
                bool directionReleased = direction != EDirectionInput.None && input.Direction.WasLeft(direction);

                if (buttonReleased || directionReleased)
                    handler();
            });
        }

        public void CloseKaraCancelWindow() => _owner.StateMachine.CloseKaraCancelWindow(_currentMove.Type);

        public void OverrideKaraCancel(bool enabled) => _owner.StateMachine.OverrideKaraCancel(enabled);

        public void SetIASA(bool enabled) => _owner.StateMachine.SetIASA(enabled);

        public void SetHitData(HitData hitData) => _owner.StateMachine.SetHitData(hitData);

        /// <summary>Is the running move in an IASA window (cancelable into anything)?</summary>
        public bool IsIASA => IsRunning && _owner.StateMachine.IASAEnabled;


        private static ButtonState GetButtonState(TickInput input, EButtonInput button) => button switch
        {
            EButtonInput.Light => input.LightAttack,
            EButtonInput.Medium => input.MediumAttack,
            EButtonInput.Heavy => input.HeavyAttack,
            EButtonInput.Unique => input.UniqueAttack,
            _ => default
        };

        // ── Termination ────────────────────────────────────────────────────────────────

        public void Cancel()
        {
            Finish();
        }

        private void Finish()
        {
            if (!IsRunning) return;
            IsRunning = false;

            var finishedMove = _currentMove;
            _currentMove = null;
            _script = null;
            _currentPose = null;

            foreach (var h in finishedMove.OnExitHandlers) h();
            finishedMove.OnMoveExit();

            // Physics overrides set inside Script() must never leak to the next move
            _owner.CharacterController.ResetPhysicsOverrides();

            OnMoveFinished?.Invoke(finishedMove);
        }
    }
}