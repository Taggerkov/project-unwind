using System;
using Systems.Combat.Combatant.Behaviour;
using Systems.Combat.Combatant.StateMachine;
using Systems.Common;
using Systems.Input;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Systems.CPU
{
    /// <summary>
    /// AI-driven <see cref="IInputProvider"/> that controls a combatant's direction and button inputs
    /// each tick. Cycles through three behavioural phases — Reposition, ExecuteMotion, CoolDown —
    /// and responds to opponent move events to decide whether to guard or counter. Attack selection is
    /// range-filtered and probability-gated via <see cref="CpuPersonality"/> tuning parameters.
    /// </summary>
    public sealed class CpuInputProvider : IInputProvider
    {
        // ── Configuration ──────────────────────────────────────────────────────────────

        /// <summary>The combatant this AI drives.</summary>
        private readonly CombatantBehaviour _self;

        /// <summary>The combatant being fought; used for spatial calculations and move-event subscriptions.</summary>
        private readonly CombatantBehaviour _opponent;

        /// <summary>Tuning parameters: aggression, guard sensitivity, preferred distance, cooldowns, etc.</summary>
        private readonly CpuPersonality _personality;

        /// <summary>Per-move attack data; null means the AI can only walk and block.</summary>
        private readonly CpuMoveHintSheet _hintSheet;

        /// <summary>Per-move defence hints consulted on each opponent move start.</summary>
        private readonly CpuDefenceHintSheet _defenceSheet;

        // ── IInputProvider ──────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public InputBuffer Buffer { get; } = new();

        /// <inheritdoc/>
        public EInputProviderType ProviderType => EInputProviderType.Cpu;

        // ── AI state ────────────────────────────────────────────────────────────────────

        /// <summary>Behavioural phase that gates what the AI may do on a given tick.</summary>
        private enum Phase
        {
            /// <summary>Walking toward or away from the opponent to reach <see cref="CpuPersonality.PreferredDistance"/>.</summary>
            Reposition,
            /// <summary>Feeding a multi-step motion sequence to the input buffer.</summary>
            ExecuteMotion,
            /// <summary>Waiting for <see cref="_globalCooldown"/> to expire before attempting another attack.</summary>
            CoolDown
        }

        /// <summary>Current behavioural phase.</summary>
        private Phase _phase = Phase.Reposition;

        /// <summary>Ticks remaining before the next attack attempt is allowed.</summary>
        private int _globalCooldown = 0;

        /// <summary>Ticks remaining before the AI acts on a detected threat; simulates human reaction time.</summary>
        private int _reactionDelay = 0;

        /// <summary>Whether the guard roll has already been committed for the current threat.</summary>
        private bool _guardDecided = false;

        /// <summary>Result of the guard roll for the current threat; false means the AI chose not to block.</summary>
        private bool _guardThisThreat = false;

        /// <summary>Direction output from the previous tick; used to compute <see cref="DirectionState"/> deltas.</summary>
        private EDirectionInput _prevDirection = EDirectionInput.None;

        /// <summary>Button flags from the previous tick; used to compute pressed/released transitions.</summary>
        private EButtonInput _prevButtons = EButtonInput.None;

        /// <summary>Motion sequencer that feeds multi-step direction inputs (e.g. QCF) over successive ticks.</summary>
        private readonly CpuMotionPlayer _motion = new();

        /// <summary>Move hint selected for the current attack sequence; null when not attacking.</summary>
        private CpuMoveHintEntry _pendingAttack;

        // ── Threat tracking ──────────────────────────────────────────────────────────────

        /// <summary>True while the opponent's active move has been classified as a threat.</summary>
        private bool _threatActive = false;

        /// <summary>Cached move reference used during threat tracking; null when no threat is active.</summary>
        private CombatantMove _currentThreatMove = null;

        /// <summary>Defence hint resolved for the current threat at move-start.</summary>
        private CpuDefenceHintEntry _pendingDefence;

        /// <summary>True after the AI commits to blocking; remains set until blockstun ends via <see cref="HandleSelfBlockStunEnded"/>.</summary>
        private bool _committedToBlock = false;

        // ── Construction ────────────────────────────────────────────────────────────────

        /// <summary>Creates a CPU provider and subscribes to combatant move events to track threats.</summary>
        /// <param name="self">The combatant this AI is controlling.</param>
        /// <param name="opponent">The combatant being fought.</param>
        /// <param name="personality">Tuning data; must not be null.</param>
        /// <param name="hintSheet">Per-move attack hints; may be null for a purely defensive/walking AI.</param>
        /// <param name="defenceSheet">Per-move defence hints; may be null to skip all threat classification.</param>
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

        /// <summary>Unsubscribes all combatant event listeners; call this when the AI is torn down.</summary>
        public void Dispose()
        {
            _self.OnBlockstunEnded -= HandleSelfBlockStunEnded;
            _opponent.Runner.OnMoveStarted -= HandleOpponentMoveStarted;
            _opponent.Runner.OnMoveFinished -= HandleOpponentMoveFinished;
        }

        /// <summary>
        /// Clears the block commitment and guard roll on blockstun exit so the next threat
        /// gets a fresh decision rather than inheriting the one that caused this block.
        /// </summary>
        private void HandleSelfBlockStunEnded()
        {
            _committedToBlock = false;
            // Also clear the guard decision so the next threat gets a fresh roll,
            // rather than inheriting the decision that caused this block.
            _guardDecided = false;
            _guardThisThreat = false;
        }

        /// <summary>
        /// Resolves the defence entry and classifies the opponent's new move as a threat or not.
        /// Unknown moves and explicitly Ignored entries are treated as non-threats.
        /// Starts the reaction delay timer when a new threat is detected.
        /// </summary>
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

        /// <summary>Clears all threat state when the opponent's move ends.</summary>
        private void HandleOpponentMoveFinished(CombatantMove move)
        {
            _threatActive = false;
            _currentThreatMove = null;
            _guardDecided = false;
            _guardThisThreat = false;
            _pendingDefence = null;
        }

        // ── IInputProvider.UpdateFrameInput ─────────────────────────────────────────────

        /// <summary>
        /// Called once per tick by the input system. Ticks cooldowns, runs the decision tree,
        /// writes the result to <see cref="Buffer"/>, and returns the computed <see cref="TickInput"/>.
        /// </summary>
        public TickInput UpdateFrameInput()
        {
            TickCooldowns();

            var (direction, buttons) = Think();
            var tick = BuildTickInput(direction, buttons);

            Buffer.Write(tick);

            _prevDirection = direction;
            _prevButtons = buttons;

            return tick;
        }

        // ── Main decision ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Priority-ordered decision tree executed each tick. Returns the direction and button flags
        /// the AI wants to output this frame. Priorities: stunned → in-motion → defend → attack → reposition.
        /// </summary>
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
        /// Returns true and sets <paramref name="response"/> when the AI should defend this tick:
        /// a threat is active, the reaction delay has expired, and the guard roll succeeded.
        /// Resets the guard decision when no threat is present so the next threat gets a fresh roll.
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

        /// <summary>
        /// Translates a confirmed <see cref="EDefenceResponse"/> into concrete direction and button outputs.
        /// CounterMove falls back to guarding when no valid counter entry exists or the counter is on cooldown.
        /// </summary>
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

        /// <summary>Returns true with probability proportional to <see cref="CpuPersonality.Aggression"/>.</summary>
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

        /// <summary>
        /// Starts the attack sequence for <paramref name="hint"/>: applies cooldowns, sets the phase,
        /// and either outputs an instant button press (no motion required) or starts the
        /// <see cref="_motion"/> sequence and returns its first step.
        /// </summary>
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

            bool facingRight = _self.StateMachine.FacingDirection == EFacingDirection.Right;
            _motion.StartMotion(hint.RequiredMotion, facingRight);

            var (dir, press) = _motion.Advance();
            var btn = press ? hint.Button : EButtonInput.None;
            return (dir, btn);
        }

        // ── Repositioning ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the direction input that moves toward the preferred engagement range.
        /// Idles when within the distance tolerance band.
        /// </summary>
        private EDirectionInput RepositionDirection(float distance)
        {
            float target = _personality.PreferredDistance;
            float tolerance = _personality.DistanceTolerance;

            if (distance > target + tolerance) return DirectionToward();
            if (distance < target - tolerance) return DirectionAway();
            return EDirectionInput.Input5;
        }

        // ── Cooldown ticking ──────────────────────────────────────────────────────────

        /// <summary>
        /// Decrements all cooldown counters and advances the phase from CoolDown to Reposition
        /// once the global cooldown expires. Also decrements per-move cooldowns in the hint sheet.
        /// </summary>
        private void TickCooldowns()
        {
            if (_globalCooldown > 0) _globalCooldown--;
            if (_reactionDelay > 0) _reactionDelay--;

            if (_phase == Phase.CoolDown && _globalCooldown <= 0)
                _phase = Phase.Reposition;

            if (_hintSheet == null) return;
            foreach (var entry in _hintSheet.Entries)
                if (entry.RemainingCooldown > 0)
                    entry.RemainingCooldown--;
        }

        // ── Spatial helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Converts raw direction and button flags into a <see cref="TickInput"/> by computing
        /// pressed/released transitions against the previous tick's outputs.
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

        /// <summary>Returns a <see cref="ButtonState"/> for <paramref name="flag"/> based on the current and previous button masks.</summary>
        private ButtonState BuildButtonState(EButtonInput flag, EButtonInput current)
        {
            bool heldNow = (current & flag) != 0;
            bool heldBefore = (_prevButtons & flag) != 0;
            return new ButtonState
            {
                Held = heldNow,
                Pressed = heldNow && !heldBefore,
                Released = !heldNow && heldBefore,
            };
        }

        /// <summary>Returns the absolute horizontal distance between the two combatants.</summary>
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

        /// <summary>True when this combatant is in hitstun or blockstun and cannot act.</summary>
        private bool IsSelfStunned()
        {
            var cs = _self.StateMachine.CombatState;
            return cs is ECombatState.Hitstun or ECombatState.Blockstun;
        }
    }
}