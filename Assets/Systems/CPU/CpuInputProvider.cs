using System;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.StateMachine;
using Systems.Common;
using Systems.Input;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Systems.CPU
{
    public sealed class CpuInputProvider : IInputProvider
    {
        // ── Configuration ──────────────────────────────────────────────────────────────

        private readonly CombatantBehaviour _self;
        private readonly CombatantBehaviour _opponent;
        private readonly CpuPersonality _personality;
        private readonly CpuMoveHintSheet _hintSheet; // may be null → AI only walks/blocks
        private readonly CpuDefenceHintSheet _defenceSheet;

        // ── IInputProvider ──────────────────────────────────────────────────────────────
        public InputBuffer Buffer { get; } = new();
        public EInputProviderType ProviderType => EInputProviderType.Cpu;

        // ── AI state ────────────────────────────────────────────────────────────────────

        private enum Phase
        {
            Reposition,
            ExecuteMotion,
            CoolDown
        }

        private Phase _phase = Phase.Reposition;

        private int _globalCooldown = 0; // ticks until next attack attempt is allowed
        private int _reactionDelay = 0; // ticks remaining before we act on a seen threat
        private bool _guardDecided = false; // remembered guard roll for the current Active phase
        private bool _guardThisThreat = false;

        private EDirectionInput _prevDirection = EDirectionInput.None;
        private EButtonInput _prevButtons = EButtonInput.None;

        private readonly CpuMotionPlayer _motion = new();
        private CpuMoveHintEntry _pendingAttack;

        // ── Threat tracking ──────────────────────────────────────────────────────────────
        private bool _threatActive = false;
        private CombatantMove _currentThreatMove = null; // used for defence sheet lookup
        private CpuDefenceHintEntry _pendingDefence;
        private bool _committedToBlock = false;

        // ── Construction ────────────────────────────────────────────────────────────────

        /// <param name="self">The combatant this AI is controlling.</param>
        /// <param name="opponent">The combatant being fought.</param>
        /// <param name="personality">Tuning data. Must not be null.</param>
        /// <param name="hintSheet">Per-move attack hints. May be null for a purely defensive/walking AI.</param>
        /// <param name="defenceSheet">Per-move defence hints.
        public CpuInputProvider(
            CombatantBehaviour self,
            CombatantBehaviour opponent,
            CpuPersonality personality,
            CpuMoveHintSheet hintSheet = null,
            CpuDefenceHintSheet defenceSheet = null)
        {
            _self = self ?? throw new ArgumentNullException(nameof(self));
            _opponent = opponent ?? throw new ArgumentNullException(nameof(opponent));
            _personality = personality ?? throw new ArgumentNullException(nameof(personality));
            _hintSheet = hintSheet;
            _defenceSheet = defenceSheet;

            _self.OnBlockstunEnded += HandleSelfBlockStunEnded;
            _opponent.Runner.OnMoveStarted += HandleOpponentMoveStarted;
            _opponent.Runner.OnMoveFinished += HandleOpponentMoveFinished;
        }

// Call this when the AI is torn down to avoid leaked subscriptions.
        public void Dispose()
        {
            _self.OnBlockstunEnded -= HandleSelfBlockStunEnded;
            _opponent.Runner.OnMoveStarted -= HandleOpponentMoveStarted;
            _opponent.Runner.OnMoveFinished -= HandleOpponentMoveFinished;
        }

        private void HandleSelfBlockStunEnded()
        {
            _committedToBlock = false;
            // Also clear the guard decision so the next threat gets a fresh roll,
            // rather than inheriting the decision that caused this block.
            _guardDecided = false;
            _guardThisThreat = false;
        }

        private void HandleOpponentMoveStarted(CombatantMove move)
        {
            // Resolve the defence entry at move-start so we know whether to treat this as a threat.
            // Unknown moves (null) and explicitly Ignored ones are both non-threats.
            _pendingDefence = _defenceSheet?.FindBestResponse(move);

            bool isThreat = _pendingDefence != null && _pendingDefence.Response != EDefenceResponse.Ignore;

            if (!isThreat)
            {
                _threatActive = false;
                return;
            }

            _threatActive = true;

            if (!_guardDecided)
                _reactionDelay = _personality.ReactionDelayTicks;
        }

        private void HandleOpponentMoveFinished(CombatantMove move)
        {
            _threatActive = false;
            _currentThreatMove = null;
            _guardDecided = false;
            _guardThisThreat = false;
            _pendingDefence = null;
        }

        // ── IInputProvider.UpdateFrameInput ─────────────────────────────────────────────

        public TickInput UpdateFrameInput()
        {
            TickCooldowns();

            var (direction, buttons) = Think();
            var tick = BuildTickInput(direction, buttons);

            Buffer.Write(tick);

            // Update history after writing — these become "previous" on the next tick.
            _prevDirection = direction;
            _prevButtons = buttons;

            return tick;
        }

        // ── Main decision ────────────────────────────────────────────────────────────────

        private (EDirectionInput direction, EButtonInput buttons) Think()
        {
            // ── Priority 1: stunned ───────────────────────────────────────────────────
            if (IsSelfStunned())
            {
                _motion.Cancel();
                _pendingAttack = null;
                _phase = Phase.Reposition;

                return _committedToBlock
                    ? (DirectionAway(), EButtonInput.None)
                    : (EDirectionInput.Input5, EButtonInput.None);
            }

            // ── Priority 2: complete an in-progress motion sequence ───────────────────
            if (_motion.IsPlaying)
            {
                var (motionDir, pressButton) = _motion.Advance();
                var btn = (pressButton && _pendingAttack != null)
                    ? _pendingAttack.Button
                    : EButtonInput.None;
                return (motionDir, btn);
            }

            // ── Priority 3: defend ────────────────────────────────────────────────────
            if (TryGetDefenceResponse(out var defenceResponse))
                return ExecuteDefenceResponse(defenceResponse);

            float distance = DistanceToOpponent();

            // ── Priority 4: attack ────────────────────────────────────────────────────
            if (_phase != Phase.CoolDown && _globalCooldown <= 0 && _reactionDelay <= 0)
            {
                var hint = PickBestAttack(distance);
                if (hint != null && RollAggression())
                    return BeginMove(hint);
            }

            // ── Priority 5: reposition ────────────────────────────────────────────────
            _phase = Phase.Reposition;
            return (RepositionDirection(distance), EButtonInput.None);
        }

        // ── Threat handling ───────────────────────────────────────────────────────────

        /// <summary>
        /// </summary>
        private bool TryGetDefenceResponse(out EDefenceResponse response)
        {
            response = EDefenceResponse.Guard; // default if we decide to defend

            if (!_threatActive)
            {
                _guardDecided = false;
                _guardThisThreat = false;
                return false;
            }

            if (!_guardDecided)
            {
                _guardThisThreat = Random.Range(0, 100) < _personality.GuardSensitivity;
                _guardDecided = true;
            }

            if (!_guardThisThreat || _reactionDelay > 0) return false;

            response = _pendingDefence?.Response ?? EDefenceResponse.Guard;
            return true;
        }

        private (EDirectionInput, EButtonInput) ExecuteDefenceResponse(EDefenceResponse response)
        {
            switch (response)
            {
                case EDefenceResponse.Guard:
                    _committedToBlock = true;
                    return (DirectionAway(), EButtonInput.None);

                case EDefenceResponse.CounterMove:
                    if (_pendingDefence != null && _hintSheet != null)
                    {
                        var counter = _hintSheet.FindHint(_pendingDefence.CounterMoveType);
                        if (counter != null && counter.RemainingCooldown <= 0)
                        {
                            DismissThreat();
                            return BeginMove(counter);
                        }
                    }

                    return (DirectionAway(), EButtonInput.None);

                default:
                    return (EDirectionInput.Input5, EButtonInput.None);
            }
        }

        /// <summary>
        /// Marks the current threat as handled without waiting for OnMoveEnded.
        /// The move may still be running in recovery — we simply no longer care.
        /// </summary>
        private void DismissThreat()
        {
            _threatActive = false;
            _pendingDefence = null;
            _guardDecided = false;
            _guardThisThreat = false;
        }

        // ── Attack ────────────────────────────────────────────────────────────────────

        private bool RollAggression()
            => Random.Range(0, 100) < _personality.Aggression;

        /// <summary>
        /// Selects the highest-priority move whose range bracket contains
        /// <paramref name="distance"/> and whose per-move cooldown has expired.
        /// </summary>
        private CpuMoveHintEntry PickBestAttack(float distance)
        {
            if (_hintSheet == null) return null;

            CpuMoveHintEntry best = null;
            int bestPri = -1;

            foreach (var entry in _hintSheet.Entries)
            {
                if (entry.RemainingCooldown > 0) continue;
                if (distance < entry.RangeMin) continue;
                if (distance > entry.RangeMax) continue;
                if (entry.Priority <= bestPri) continue;

                best = entry;
                bestPri = entry.Priority;
            }

            return best;
        }

        private (EDirectionInput direction, EButtonInput buttons) BeginMove(CpuMoveHintEntry hint)
        {
            _pendingAttack = hint;

            // Apply cooldowns immediately so the AI can't re-select the same move next tick
            // if the motion player hasn't started yet.
            hint.RemainingCooldown = hint.CooldownTicks;
            _globalCooldown = _personality.GlobalAttackCooldownTicks;
            _phase = Phase.ExecuteMotion;

            if (hint.RequiredMotion == EMotionInput.None)
            {
                // Instant press — no motion needed. Output button this tick and enter cooldown.
                _phase = Phase.CoolDown;
                return (EDirectionInput.Input5, hint.Button);
            }

            // Start the motion sequence; Advance() will feed directions over the coming ticks.
            bool facingRight = _self.StateMachine.FacingDirection == EFacingDirection.Right;
            _motion.StartMotion(hint.RequiredMotion, facingRight);

            // Return the first step of the motion immediately.
            var (dir, press) = _motion.Advance();
            var btn = press ? hint.Button : EButtonInput.None;
            return (dir, btn);
        }

        // ── Repositioning ─────────────────────────────────────────────────────────────

        private EDirectionInput RepositionDirection(float distance)
        {
            float target = _personality.PreferredDistance;
            float tolerance = _personality.DistanceTolerance;

            if (distance > target + tolerance) return DirectionToward(); // too far → close in
            if (distance < target - tolerance) return DirectionAway(); // too close → back off
            return EDirectionInput.Input5; // in the sweet spot → idle
        }

        // ── Cooldown ticking ──────────────────────────────────────────────────────────

        private void TickCooldowns()
        {
            if (_globalCooldown > 0) _globalCooldown--;
            if (_reactionDelay > 0) _reactionDelay--;

            // Phase transitions
            if (_phase == Phase.CoolDown && _globalCooldown <= 0)
                _phase = Phase.Reposition;

            if (_hintSheet == null) return;
            foreach (var entry in _hintSheet.Entries)
                if (entry.RemainingCooldown > 0)
                    entry.RemainingCooldown--;
        }

        // ── Spatial helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Converts raw AI intent (flat direction + button flags) into a fully temporal
        /// TickInput by comparing against the previous frame's outputs.
        /// </summary>
        private TickInput BuildTickInput(EDirectionInput direction, EButtonInput buttons)
        {
            return new TickInput
            {
                Direction = new DirectionState(direction, _prevDirection),
                LightAttack = BuildButtonState(EButtonInput.Light, buttons),
                MediumAttack = BuildButtonState(EButtonInput.Medium, buttons),
                HeavyAttack = BuildButtonState(EButtonInput.Heavy, buttons),
                UniqueAttack = BuildButtonState(EButtonInput.Unique, buttons),
                GuardButton = BuildButtonState(EButtonInput.Guard, buttons),
                AbilityButton = BuildButtonState(EButtonInput.Ability, buttons),
            };
        }

        private ButtonState BuildButtonState(EButtonInput flag, EButtonInput current)
        {
            bool heldNow = (current & flag) != 0;
            bool heldBefore = (_prevButtons & flag) != 0;
            return new ButtonState
            {
                Held = heldNow,
                Pressed = heldNow && !heldBefore, // first frame down
                Released = !heldNow && heldBefore, // first frame up
            };
        }

        private float DistanceToOpponent()
            => Mathf.Abs(_opponent.transform.position.x - _self.transform.position.x);

        /// <summary>True when the opponent's X position is to our right in world space.</summary>
        private bool OpponentIsRight()
            => _opponent.transform.position.x > _self.transform.position.x;

        /// <summary>World-space direction that will move us toward the opponent.</summary>
        private EDirectionInput DirectionToward()
            => OpponentIsRight() ? EDirectionInput.Input6 : EDirectionInput.Input4;

        /// <summary>World-space direction that will move us away from the opponent (= guard direction).</summary>
        private EDirectionInput DirectionAway()
            => OpponentIsRight() ? EDirectionInput.Input4 : EDirectionInput.Input6;

        // ── State queries ─────────────────────────────────────────────────────────────

        private bool IsSelfStunned()
        {
            var cs = _self.StateMachine.CombatState;
            return cs is ECombatState.Hitstun or ECombatState.Blockstun;
        }
    }
}