using System.Collections.Generic;
using Systems.Input;

namespace Systems.CPU
{
    /// <summary>
    /// Plays out fighting-game motion inputs (QCF, DP, charge, etc.) over several ticks
    /// by outputting a pre-planned direction sequence that MotionMatcher can recognise.
    ///
    /// The AI calls <see cref="StartMotion"/> once, then <see cref="Advance"/> each tick
    /// while <see cref="IsPlaying"/> is true. The final step of the sequence returns
    /// pressButton = true, which tells the AI to also hold the attack button that tick.
    ///
    /// Directions are in world-space (Input6 = always screen-right) because
    /// CharacterInputView handles the facing flip before MotionMatcher sees the frame.
    /// </summary>
    internal sealed class CpuMotionPlayer
    {
        // ── Internal step representation ──────────────────────────────────────────────

        private readonly struct MotionStep
        {
            public readonly EDirectionInput Direction;
            public readonly int HoldTicks;
            public readonly bool PressButtonOnLastTick;

            public MotionStep(EDirectionInput dir, int hold, bool pressButton = false)
            {
                Direction = dir;
                HoldTicks = hold;
                PressButtonOnLastTick = pressButton;
            }
        }

        // ── State ─────────────────────────────────────────────────────────────────────

        private readonly Queue<MotionStep> _pending = new();
        private EDirectionInput _currentDir = EDirectionInput.Input5;
        private int _holdRemaining = 0;
        private bool _pressButtonThisStep = false;

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>True while the motion sequence has not fully played out.</summary>
        public bool IsPlaying => _pending.Count > 0 || _holdRemaining > 0;

        /// <summary>Immediately abandons any in-progress sequence.</summary>
        public void Cancel()
        {
            _pending.Clear();
            _holdRemaining = 0;
            _pressButtonThisStep = false;
        }

        /// <summary>
        /// Enqueues the direction sequence for <paramref name="motion"/>.
        /// <paramref name="facingRight"/> determines which world-space directions map to
        /// "forward" and "backward" (Input6/Input4 vs Input4/Input6).
        /// </summary>
        public void StartMotion(EMotionInput motion, bool facingRight)
        {
            Cancel();

            // World-space direction aliases — facing-aware.
            var fwd = facingRight ? EDirectionInput.Input6 : EDirectionInput.Input4;
            var back = facingRight ? EDirectionInput.Input4 : EDirectionInput.Input6;
            var fwdDown = facingRight ? EDirectionInput.Input3 : EDirectionInput.Input1;
            var backDown = facingRight ? EDirectionInput.Input1 : EDirectionInput.Input3;

            // MotionMatcher's MotionWindow is 20 ticks, so all sequences below fit comfortably.
            // ChargeFrames = 30, so charge sequences hold for 35 ticks (5-tick margin).
            switch (motion)
            {
                // ── Standard motions ──────────────────────────────────────────────────

                case EMotionInput.Held8:
                    Step(EDirectionInput.Input8, 1, pressButton: true);
                    break;
                case EMotionInput.Held2:
                    Step(EDirectionInput.Input2, 1, pressButton: true);
                    break;
                case EMotionInput.Held4:
                    Step(back, 1, pressButton: true);
                    break;
                case EMotionInput.Held6:
                    Step(fwd, 1, pressButton: true);
                    break;

                case EMotionInput.QCF:
                    // 2 → 3 → 6 + button
                    Step(EDirectionInput.Input2, 3);
                    Step(fwdDown, 2);
                    Step(fwd, 1, pressButton: true);
                    break;

                case EMotionInput.QCB:
                    // 2 → 1 → 4 + button
                    Step(EDirectionInput.Input2, 3);
                    Step(backDown, 2);
                    Step(back, 1, pressButton: true);
                    break;

                case EMotionInput.DP:
                    // 6 → 2 → 3 + button  (623)
                    Step(fwd, 2);
                    Step(EDirectionInput.Input2, 2);
                    Step(fwdDown, 1, pressButton: true);
                    break;

                case EMotionInput.RDP:
                    // 4 → 2 → 1 + button  (421)
                    Step(back, 2);
                    Step(EDirectionInput.Input2, 2);
                    Step(backDown, 1, pressButton: true);
                    break;

                case EMotionInput.HCF:
                    // 4 → 1 → 2 → 3 → 6 + button
                    Step(back, 3);
                    Step(backDown, 2);
                    Step(EDirectionInput.Input2, 2);
                    Step(fwdDown, 2);
                    Step(fwd, 1, pressButton: true);
                    break;

                case EMotionInput.HCB:
                    // 6 → 3 → 2 → 1 → 4 + button
                    Step(fwd, 3);
                    Step(fwdDown, 2);
                    Step(EDirectionInput.Input2, 2);
                    Step(backDown, 2);
                    Step(back, 1, pressButton: true);
                    break;

                // ── Charge inputs ──────────────────────────────────────────────────────

                case EMotionInput.Charge46:
                    // Hold back 35 ticks (> ChargeFrames=30) → forward + button
                    Step(back, 35);
                    Step(fwd, 1, pressButton: true);
                    break;

                case EMotionInput.Charge64:
                    Step(fwd, 35);
                    Step(back, 1, pressButton: true);
                    break;

                case EMotionInput.Charge28:
                    Step(EDirectionInput.Input2, 35);
                    Step(EDirectionInput.Input8, 1, pressButton: true);
                    break;

                case EMotionInput.Charge82:
                    Step(EDirectionInput.Input8, 35);
                    Step(EDirectionInput.Input2, 1, pressButton: true);
                    break;

                // ── Double-tap (handled as two quick direction taps) ───────────────────

                case EMotionInput.DoubleTap6:
                    Step(fwd, 2);
                    Step(EDirectionInput.Input5, 2);
                    Step(fwd, 2, pressButton: true);
                    break;

                case EMotionInput.DoubleTap4:
                    Step(back, 2);
                    Step(EDirectionInput.Input5, 2);
                    Step(back, 2, pressButton: true);
                    break;

                default:
                    // None or unrecognised — just press the button with no motion.
                    // The caller should have handled EMotionInput.None directly; this
                    // branch is a safe fallback.
                    Step(EDirectionInput.Input5, 1, pressButton: true);
                    break;
            }
        }

        /// <summary>
        /// Advances the sequence by one tick. Call each tick while <see cref="IsPlaying"/>.
        /// Returns the world-space direction to hold and whether the attack button should
        /// be pressed this tick.
        /// </summary>
        public (EDirectionInput direction, bool pressButton) Advance()
        {
            // Consume the next step when the current one expires.
            if (_holdRemaining <= 0 && _pending.Count > 0)
            {
                var step = _pending.Dequeue();
                _currentDir = step.Direction;
                _holdRemaining = step.HoldTicks;
                _pressButtonThisStep = step.PressButtonOnLastTick;
            }

            if (_holdRemaining > 0)
            {
                _holdRemaining--;
                // Only press the button on the very last tick of the final step
                // so the button edge falls exactly when MotionMatcher expects it.
                bool press = _pressButtonThisStep && _holdRemaining == 0 && _pending.Count == 0;
                return (_currentDir, press);
            }

            return (EDirectionInput.Input5, false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private void Step(EDirectionInput dir, int ticks, bool pressButton = false)
            => _pending.Enqueue(new MotionStep(dir, ticks, pressButton));
    }
}