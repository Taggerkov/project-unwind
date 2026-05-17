using System.Collections.Generic;

namespace Systems.Input
{
    public static class MotionMatcher
    {
        // ── Tuning ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// How many ticks back to search for a button press edge.
        /// This is the forgiveness window — important for wakeup options and cancels.
        /// </summary>
        private const int ButtonEdgeLeniency = 3;

        /// <summary>Total tick window a motion sequence must fit within from the anchor.</summary>
        private const int MotionWindow = 20;

        /// <summary>Window for double-tap detection.</summary>
        private const int DoubleTapWindow = 12;

        /// <summary>
        /// Minimum frames a charge direction must be held continuously before release.
        /// 30 frames = 0.5s at 60fps, standard for most fighting games.
        /// </summary>
        private const int ChargeFrames = 30;

        /// <summary>
        /// How many frames of non-charge direction are tolerated between the charge hold
        /// ending and the button press. Covers the brief transition to the release direction.
        /// </summary>
        private const int ChargeTransitionWindow = 8;

        // ── Public API ────────────────────────────────────────────────────────────────

        public static bool AnyMatch(IInputView buffer, IReadOnlyList<MoveInputDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
                if (Matches(buffer, descriptor))
                    return true;
            return false;
        }

        /// <summary>
        /// Returns true when ALL descriptors in the entry match simultaneously.
        /// This is the AND-clause evaluation for a single input alternative.
        /// </summary>
        public static bool Matches(IInputView buffer, MoveInputEntry entry)
        {
            if (entry.Conditions == null || entry.Conditions.Count == 0) return false;

            foreach (var descriptor in entry.Conditions)
                if (!Matches(buffer, descriptor))
                    return false;

            return true;
        }

        public static bool Matches(IInputView buffer, MoveInputDescriptor descriptor)
        {
            if (descriptor.Buttons == EButtonInput.None)
                return MatchNoButton(buffer, descriptor.Motion);
            else
                return MatchWithButton(buffer, descriptor);
        }

        // ── Path A: no button — direction state or edge is the trigger ────────────────

        private static bool MatchNoButton(IInputView buffer, EMotionInput motion)
        {
            var f0 = buffer.GetFrame(0);

            return motion switch
            {
                // ── Held ──────────────────────────────────────────────────────────────
                EMotionInput.Held4 => f0.HasDirection(EDirectionInput.Input4),
                EMotionInput.Held6 => f0.HasDirection(EDirectionInput.Input6),
                EMotionInput.Held2 => f0.HasDirection(EDirectionInput.Input2),
                EMotionInput.Held8 => f0.HasDirection(EDirectionInput.Input8),

                EMotionInput.HeldAnyBack => f0.HasDirection(EDirectionInput.Input4) ||
                                            f0.HasDirection(EDirectionInput.Input1) ||
                                            f0.HasDirection(EDirectionInput.Input7),

                EMotionInput.HeldAnyForward => f0.HasDirection(EDirectionInput.Input6) ||
                                               f0.HasDirection(EDirectionInput.Input3) ||
                                               f0.HasDirection(EDirectionInput.Input9),

                EMotionInput.HeldAnyDown => f0.HasDirection(EDirectionInput.Input2) ||
                                            f0.HasDirection(EDirectionInput.Input1) ||
                                            f0.HasDirection(EDirectionInput.Input3),

                EMotionInput.HeldAnyUp => f0.HasDirection(EDirectionInput.Input8) ||
                                          f0.HasDirection(EDirectionInput.Input7) ||
                                          f0.HasDirection(EDirectionInput.Input9),

                // ── Disallow (NOT held) ───────────────────────────────────────────────
                EMotionInput.DisallowAnyBack =>
                    !f0.HasDirection(EDirectionInput.Input4) &&
                    !f0.HasDirection(EDirectionInput.Input1) &&
                    !f0.HasDirection(EDirectionInput.Input7),

                EMotionInput.DisallowAnyForward =>
                    !f0.HasDirection(EDirectionInput.Input6) &&
                    !f0.HasDirection(EDirectionInput.Input3) &&
                    !f0.HasDirection(EDirectionInput.Input9),

                EMotionInput.DisallowAnyDown =>
                    !f0.HasDirection(EDirectionInput.Input2) &&
                    !f0.HasDirection(EDirectionInput.Input1) &&
                    !f0.HasDirection(EDirectionInput.Input3),

                EMotionInput.DisallowAnyUp =>
                    !f0.HasDirection(EDirectionInput.Input8) &&
                    !f0.HasDirection(EDirectionInput.Input7) &&
                    !f0.HasDirection(EDirectionInput.Input9),

                // ── Double-tap ────────────────────────────────────────────────────────
                EMotionInput.DoubleTap4 => MatchDoubleTapNoButton(buffer, EDirectionInput.Input4),
                EMotionInput.DoubleTap6 => MatchDoubleTapNoButton(buffer, EDirectionInput.Input6),
                EMotionInput.DoubleTap2 => MatchDoubleTapNoButton(buffer, EDirectionInput.Input2),
                EMotionInput.DoubleTap8 => MatchDoubleTapNoButton(buffer, EDirectionInput.Input8),

                // ── None: truly neutral ───────────────────────────────────────────────
                EMotionInput.None => true,

                _ => false,
            };
        }

        // ── Path B: button-anchored — button edge is the trigger ─────────────────────

        private static bool MatchWithButton(IInputView buffer, MoveInputDescriptor descriptor)
        {
            int anchor = FindButtonEdge(buffer, descriptor.Buttons, ButtonEdgeLeniency);
            if (anchor < 0) return false;

            return descriptor.Motion switch
            {
                EMotionInput.None => true,

                // ── Held — direction must be active on the same tick as the button press ──
                EMotionInput.Held4 => buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input4),
                EMotionInput.Held6 => buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input6),
                EMotionInput.Held2 => buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input2),
                EMotionInput.Held8 => buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input8),

                // ── HeldAny — same as Held but accepts diagonals ──────────────────────
                EMotionInput.HeldAnyBack =>
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input4) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input1) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input7),

                EMotionInput.HeldAnyForward =>
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input6) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input3) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input9),

                EMotionInput.HeldAnyDown =>
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input2) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input1) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input3),

                EMotionInput.HeldAnyUp =>
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input8) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input7) ||
                    buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input9),

                // ── Disallow at anchor — direction must NOT be held at the button press ─
                EMotionInput.DisallowAnyBack =>
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input4) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input1) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input7),

                EMotionInput.DisallowAnyForward =>
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input6) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input3) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input9),

                EMotionInput.DisallowAnyDown =>
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input2) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input1) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input3),

                EMotionInput.DisallowAnyUp =>
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input8) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input7) &&
                    !buffer.GetFrame(anchor).HasDirection(EDirectionInput.Input9),

                // ── Double-tap ────────────────────────────────────────────────────────
                EMotionInput.DoubleTap4 => MatchDoubleTapWithButton(buffer, anchor, EDirectionInput.Input4),
                EMotionInput.DoubleTap6 => MatchDoubleTapWithButton(buffer, anchor, EDirectionInput.Input6),
                EMotionInput.DoubleTap2 => MatchDoubleTapWithButton(buffer, anchor, EDirectionInput.Input2),
                EMotionInput.DoubleTap8 => MatchDoubleTapWithButton(buffer, anchor, EDirectionInput.Input8),

                // ── Motion sequences ──────────────────────────────────────────────────
                EMotionInput.QCF => MatchSequence(buffer, anchor, QcfSteps),
                EMotionInput.QCB => MatchSequence(buffer, anchor, QcbSteps),
                EMotionInput.DP => MatchSequence(buffer, anchor, DpSteps),
                EMotionInput.RDP => MatchSequence(buffer, anchor, RdpSteps),
                EMotionInput.HCF => MatchSequence(buffer, anchor, HcfSteps),
                EMotionInput.HCB => MatchSequence(buffer, anchor, HcbSteps),
                EMotionInput.FC => MatchFullCircle(buffer, anchor),

                // ── Charge inputs ─────────────────────────────────────────────────────
                EMotionInput.Charge46 => MatchCharge(buffer, anchor, ChargeBack, ChargeForward),
                EMotionInput.Charge64 => MatchCharge(buffer, anchor, ChargeForward, ChargeBack),
                EMotionInput.Charge28 => MatchCharge(buffer, anchor, ChargeDown, ChargeUp),
                EMotionInput.Charge82 => MatchCharge(buffer, anchor, ChargeUp, ChargeDown),

                _ => false,
            };
        }

        // ── Charge direction sets (include diagonals for leniency) ────────────────────

        private static readonly EDirectionInput[] ChargeBack =
            { EDirectionInput.Input4, EDirectionInput.Input1, EDirectionInput.Input7 };

        private static readonly EDirectionInput[] ChargeForward =
            { EDirectionInput.Input6, EDirectionInput.Input3, EDirectionInput.Input9 };

        private static readonly EDirectionInput[] ChargeDown =
            { EDirectionInput.Input2, EDirectionInput.Input1, EDirectionInput.Input3 };

        private static readonly EDirectionInput[] ChargeUp =
            { EDirectionInput.Input8, EDirectionInput.Input7, EDirectionInput.Input9 };

        // ── Motion step tables ────────────────────────────────────────────────────────

        private static readonly EDirectionInput[][] QcfSteps =
        {
            new[] { EDirectionInput.Input6, EDirectionInput.Input3 },
            new[] { EDirectionInput.Input3 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input1, EDirectionInput.Input3 },
        };

        private static readonly EDirectionInput[][] QcbSteps =
        {
            new[] { EDirectionInput.Input4, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input1 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input3, EDirectionInput.Input1 },
        };

        private static readonly EDirectionInput[][] DpSteps =
        {
            new[] { EDirectionInput.Input6, EDirectionInput.Input3 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input3 },
            new[] { EDirectionInput.Input6 },
        };

        private static readonly EDirectionInput[][] RdpSteps =
        {
            new[] { EDirectionInput.Input4, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input4 },
        };

        private static readonly EDirectionInput[][] HcfSteps =
        {
            new[] { EDirectionInput.Input6, EDirectionInput.Input3 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input3, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input1, EDirectionInput.Input4 },
            new[] { EDirectionInput.Input4 },
        };

        private static readonly EDirectionInput[][] HcbSteps =
        {
            new[] { EDirectionInput.Input4, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input2, EDirectionInput.Input3, EDirectionInput.Input1 },
            new[] { EDirectionInput.Input3, EDirectionInput.Input6 },
            new[] { EDirectionInput.Input6 },
        };

        // ── Sequence matching ─────────────────────────────────────────────────────────

        private static bool MatchSequence(IInputView buffer, int anchor, EDirectionInput[][] steps)
        {
            int cursor = anchor;
            int limit = anchor + MotionWindow;

            foreach (var step in steps)
            {
                int found = FindAnyDirection(buffer, step, cursor, limit);
                if (found < 0) return false;
                cursor = found + 1;
            }

            return true;
        }

        // ── Double-tap matching ───────────────────────────────────────────────────────

        /// <summary>
        /// No-button variant. The direction being newly pressed this tick is the trigger.
        /// Requires a clean gap between the two taps — only neutral (Input5) is permitted
        /// between them. Any other direction (e.g. the opposite direction) invalidates the match.
        /// </summary>
        private static bool MatchDoubleTapNoButton(IInputView buffer, EDirectionInput direction)
        {
            // Must be a fresh press this tick
            if (!buffer.GetFrame(0).HasDirection(direction)) return false;
            if (buffer.GetFrame(1).HasDirection(direction)) return false;

            // Find a previous tap within the window
            int previousTap = FindDirection(buffer, direction, 2, DoubleTapWindow);
            if (previousTap < 0) return false;

            // The gap between the two taps (frames 1 to previousTap-1) must contain
            // only neutral — any other direction means the player changed input in between.
            for (int t = 1; t < previousTap; t++)
            {
                var dir = buffer.GetFrame(t).Direction.Current;
                if (dir != EDirectionInput.Input5 && dir != direction)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Button-anchored variant. Looks for two separate direction taps before the button press.
        /// Requires a clean gap between the two taps — only neutral (Input5) is permitted
        /// between them.
        /// </summary>
        private static bool MatchDoubleTapWithButton(IInputView buffer, int anchor, EDirectionInput direction)
        {
            // Find the most recent tap at or before the button press
            int firstTapEnd = FindDirection(buffer, direction, anchor, DoubleTapWindow);
            if (firstTapEnd < 0) return false;

            // Skip past the continuous hold of that tap going further back
            int gapStart = firstTapEnd + 1;
            while (gapStart < buffer.Size && buffer.GetFrame(gapStart).HasDirection(direction))
                gapStart++;

            if (gapStart >= buffer.Size) return false;

            // Find the second (older) tap
            int limit = anchor + DoubleTapWindow;
            int secondTap = FindDirection(buffer, direction, gapStart, limit - gapStart);
            if (secondTap < 0) return false;

            // The gap between the two taps must contain only neutral
            for (int t = gapStart; t < secondTap; t++)
            {
                var dir = buffer.GetFrame(t).Direction.Current;
                if (dir != EDirectionInput.Input5 && dir != direction)
                    return false;
            }

            return true;
        }

        // ── Charge matching ───────────────────────────────────────────────────────────

        private static bool MatchCharge(IInputView buffer, int anchor,
            EDirectionInput[] chargeDirections, EDirectionInput[] releaseDirections)
        {
            // Step 1: the release direction must be held at the anchor frame
            if (!IsAnyDirection(buffer.GetFrame(anchor).Direction, releaseDirections))
                return false;

            // Step 2: walk backward past the release direction (transition to charge)
            int t = anchor + 1;
            int transitionLimit = System.Math.Min(anchor + ChargeTransitionWindow, buffer.Size - 1);

            while (t <= transitionLimit && !IsAnyDirection(buffer.GetFrame(t).Direction, chargeDirections))
                t++;

            if (t >= buffer.Size) return false;
            if (!IsAnyDirection(buffer.GetFrame(t).Direction, chargeDirections)) return false;

            // Step 3: count consecutive frames of the charge direction
            int chargeCount = 0;
            int chargeLimit = System.Math.Min(t + ChargeFrames + 10, buffer.Size - 1);

            while (t <= chargeLimit && IsAnyDirection(buffer.GetFrame(t).Direction, chargeDirections))
            {
                chargeCount++;
                t++;
            }

            return chargeCount >= ChargeFrames;
        }

        // ── Full circle ───────────────────────────────────────────────────────────────

        private static bool MatchFullCircle(IInputView buffer, int anchor)
        {
            int limit = System.Math.Min(anchor + MotionWindow, buffer.Size - 1);
            bool hasUp = false, hasDown = false, hasLeft = false, hasRight = false;

            for (int i = anchor; i <= limit; i++)
            {
                var dir = buffer.GetFrame(i).Direction.Current;
                hasUp |= dir is EDirectionInput.Input7 or EDirectionInput.Input8 or EDirectionInput.Input9;
                hasRight |= dir is EDirectionInput.Input9 or EDirectionInput.Input6 or EDirectionInput.Input3;
                hasDown |= dir is EDirectionInput.Input1 or EDirectionInput.Input2 or EDirectionInput.Input3;
                hasLeft |= dir is EDirectionInput.Input7 or EDirectionInput.Input4 or EDirectionInput.Input1;
            }

            return hasUp && hasDown && hasLeft && hasRight;
        }

        // ── Primitive searches ────────────────────────────────────────────────────────

        private static int FindButtonEdge(IInputView buffer, EButtonInput buttons, int maxTicksAgo)
        {
            int limit = System.Math.Min(maxTicksAgo, buffer.Size - 2);
            for (int t = 0; t <= limit; t++)
            {
                if (buffer.GetFrame(t).HasButtons(buttons) && !buffer.GetFrame(t + 1).HasButtons(buttons))
                    return t;
            }

            return -1;
        }

        private static int FindDirection(IInputView buffer, EDirectionInput direction, int startTicksAgo, int window)
        {
            int limit = System.Math.Min(startTicksAgo + window, buffer.Size - 1);
            for (int t = startTicksAgo; t <= limit; t++)
                if (buffer.GetFrame(t).HasDirection(direction))
                    return t;
            return -1;
        }

        private static int FindAnyDirection(IInputView buffer, EDirectionInput[] candidates, int startTicksAgo,
            int limit)
        {
            limit = System.Math.Min(limit, buffer.Size - 1);
            for (int t = startTicksAgo; t <= limit; t++)
            {
                var dir = buffer.GetFrame(t).Direction.Current;
                foreach (var c in candidates)
                    if (dir == c)
                        return t;
            }

            return -1;
        }

        private static bool IsAnyDirection(DirectionState dir, EDirectionInput[] candidates)
        {
            foreach (var c in candidates)
                if (dir.Current == c)
                    return true;
            return false;
        }
    }
}