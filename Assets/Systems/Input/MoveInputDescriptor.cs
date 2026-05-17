using System;
using System.Collections.Generic;

namespace Systems.Input
{
    /// <summary>
    /// A single atomic input condition: one motion sequence paired with zero or more
    /// simultaneous buttons. This is the leaf-level building block of the input system.
    /// </summary>
    [Serializable]
    public struct MoveInputDescriptor
    {
        public EMotionInput Motion;
        public EButtonInput Buttons;

        public MoveInputDescriptor(EMotionInput motion, EButtonInput buttons)
        {
            Motion = motion;
            Buttons = buttons;
        }

        /// <summary>
        /// Specificity score for this single descriptor.
        ///
        ///   None (button only):     0
        ///   Held / HeldAny:        10
        ///   Disallow (constraint):  5
        ///   DoubleTap:             15
        ///   QCF / QCB / DP / RDP: 20
        ///   Charge:                25
        ///   HCF / HCB:             30
        ///   FC:                    40
        ///   +1 per simultaneous button required (tiebreaker for identical motion)
        /// </summary>
        public int Specificity
        {
            get
            {
                int motionScore = Motion switch
                {
                    EMotionInput.None => 0,

                    EMotionInput.Held4 or EMotionInput.Held6
                        or EMotionInput.Held2 or EMotionInput.Held8
                        or EMotionInput.HeldAnyBack or EMotionInput.HeldAnyForward
                        or EMotionInput.HeldAnyDown or EMotionInput.HeldAnyUp => 10,

                    // Disallow constraints are worth something but less than a positive hold,
                    // so two descriptors with the same motion but one adding a Disallow ranks higher.
                    EMotionInput.DisallowAnyBack or EMotionInput.DisallowAnyForward
                        or EMotionInput.DisallowAnyDown or EMotionInput.DisallowAnyUp => 5,

                    EMotionInput.DoubleTap4 or EMotionInput.DoubleTap6
                        or EMotionInput.DoubleTap2 or EMotionInput.DoubleTap8 => 15,

                    EMotionInput.QCF or EMotionInput.QCB
                        or EMotionInput.DP or EMotionInput.RDP => 20,

                    EMotionInput.Charge46 or EMotionInput.Charge64
                        or EMotionInput.Charge28 or EMotionInput.Charge82 => 25,

                    EMotionInput.HCF or EMotionInput.HCB => 30,

                    EMotionInput.FC => 40,

                    _ => 0,
                };

                // Popcount — number of individual buttons required.
                int b = (int)Buttons;
                b -= (b >> 1) & 0x55555555;
                b = (b & 0x33333333) + ((b >> 2) & 0x33333333);
                b = (b + (b >> 4)) & 0x0f0f0f0f;
                int buttonScore = (b * 0x01010101) >> 24;

                return motionScore + buttonScore;
            }
        }
    }

    /// <summary>
    /// One alternative input sequence for a move. Represents the AND clause of the
    /// overall OR(AND) matching grammar:
    ///
    ///   A move matches  when ANY entry matches.
    ///   An entry matches when ALL of its descriptors match.
    ///
    /// This mirrors BBScript's behaviour where multiple moveInput lines on a single move
    /// definition must all resolve to true simultaneously.
    ///
    /// Examples:
    ///
    ///   Simple press (single descriptor):
    ///     new MoveInputEntry(EMotionInput.QCF, EButtonInput.Light)
    ///
    ///   6H with disallow-up constraint (two descriptors in one entry):
    ///     new MoveInputEntry(
    ///         new MoveInputDescriptor(EMotionInput.HeldAnyForward, EButtonInput.Heavy),
    ///         new MoveInputDescriptor(EMotionInput.DisallowAnyUp,  EButtonInput.None))
    /// </summary>
    [Serializable]
    public struct MoveInputEntry
    {
        /// <summary>All conditions that must be simultaneously true for this entry to match.</summary>
        public List<MoveInputDescriptor> Conditions;

        // ── Constructors ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Single-condition convenience constructor. Covers the vast majority of moves
        /// that require exactly one motion + button combination.
        /// </summary>
        public MoveInputEntry(EMotionInput motion, EButtonInput buttons)
        {
            Conditions = new List<MoveInputDescriptor> { new(motion, buttons) };
        }

        /// <summary>
        /// Multi-condition (AND) constructor. All supplied descriptors must match
        /// simultaneously for this entry to resolve.
        /// </summary>
        public MoveInputEntry(params MoveInputDescriptor[] conditions)
        {
            Conditions = new List<MoveInputDescriptor>(conditions);
        }

        // ── Scoring ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Total specificity of this entry — sum of all individual descriptor scores.
        /// Compound entries naturally outscore simpler ones with the same base motion
        /// because they carry additional constraint descriptors.
        /// </summary>
        public int Specificity
        {
            get
            {
                if (Conditions == null) return 0;
                int total = 0;
                foreach (var c in Conditions) total += c.Specificity;
                return total;
            }
        }

        // ── Anchor accessors ──────────────────────────────────────────────────────────
        // Used by CombatantMove to build a MoveMatchResult after a successful match.

        /// <summary>
        /// The button(s) that anchor this entry's timing — the first descriptor that
        /// requires one or more buttons, or None if this is a button-less entry.
        /// </summary>
        public EButtonInput PrimaryButton
        {
            get
            {
                if (Conditions == null) return EButtonInput.None;
                foreach (var c in Conditions)
                    if (c.Buttons != EButtonInput.None)
                        return c.Buttons;
                return EButtonInput.None;
            }
        }

        /// <summary>
        /// The dominant motion for direction resolution — the first descriptor whose
        /// motion is a positive (non-Disallow, non-None) input.
        /// </summary>
        public EMotionInput PrimaryMotion
        {
            get
            {
                if (Conditions == null) return EMotionInput.None;
                foreach (var c in Conditions)
                {
                    if (c.Motion != EMotionInput.None && !IsDisallow(c.Motion))
                        return c.Motion;
                }

                return EMotionInput.None;
            }
        }

        private static bool IsDisallow(EMotionInput m) =>
            m is EMotionInput.DisallowAnyBack or EMotionInput.DisallowAnyForward
                or EMotionInput.DisallowAnyDown or EMotionInput.DisallowAnyUp;
    }
}