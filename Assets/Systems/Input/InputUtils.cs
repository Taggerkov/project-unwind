using UnityEngine;

namespace Systems.Input
{
    /// <summary>Static helpers for converting raw input vectors and integers into game input types.</summary>
    public static class InputUtils
    {
        /// <summary>
        /// Run-length compressed representation of a <see cref="TickInput"/>; consecutive identical
        /// frames are merged into a single entry by incrementing <see cref="FrameCount"/>.
        /// </summary>
        public class CompressedInput
        {
            /// <summary>The input state captured at the start of this run.</summary>
            public TickInput TickData;

            /// <summary>Number of consecutive ticks during which this input state was held unchanged.</summary>
            public int FrameCount;

            /// <summary>
            /// Creates a new compressed entry for a single tick.
            /// </summary>
            /// <param name="tick">The input state to capture.</param>
            public CompressedInput(TickInput tick)
            {
                TickData = tick;
                FrameCount = 1;
            }

            /// <summary>
            /// Returns true if <paramref name="other"/> represents the same held input state as
            /// this entry, meaning the two ticks can be merged into the same run.
            /// </summary>
            /// <param name="other">The tick to compare against the stored state.</param>
            /// <returns>True when direction and all held-button states are identical.</returns>
            public bool Matches(TickInput other)
            {
                // If the direction or ANY button state differs, it's a new input
                return TickData.Direction.Current == other.Direction.Current &&
                       TickData.LightAttack.Held == other.LightAttack.Held &&
                       TickData.MediumAttack.Held == other.MediumAttack.Held &&
                       TickData.HeavyAttack.Held == other.HeavyAttack.Held &&
                       TickData.UniqueAttack.Held == other.UniqueAttack.Held &&
                       TickData.GuardButton.Held == other.GuardButton.Held &&
                       TickData.AbilityButton.Held == other.AbilityButton.Held;
            }
        }

        /// <summary>
        /// Converts a normalised 2D axis vector to a numpad integer (1–9, or 0 for neutral/deadzone).
        /// </summary>
        /// <param name="dir">The raw axis value, typically from an analog stick or D-pad.</param>
        /// <returns>
        /// A numpad integer matching the 8-directional layout, or 0 when the vector is within the deadzone.
        /// </returns>
        public static int VectorToNumpad(Vector2 dir)
        {
            // Use a small deadzone to avoid "floating" stick noise
            int x = dir.x > 0.3f ? 1 : (dir.x < -0.3f ? -1 : 0);
            int y = dir.y > 0.3f ? 1 : (dir.y < -0.3f ? -1 : 0);

            if (x == 0 && y == 0) return 0; // Neutral
            if (x == -1 && y == -1) return 1;
            if (x == 0 && y == -1) return 2;
            if (x == 1 && y == -1) return 3;
            if (x == -1 && y == 0) return 4;
            if (x == 1 && y == 0) return 6;
            if (x == -1 && y == 1) return 7;
            if (x == 0 && y == 1) return 8;
            if (x == 1 && y == 1) return 9;

            return 5;
        }

        /// <summary>
        /// Maps a numpad integer to the corresponding <see cref="EDirectionInput"/> enum value.
        /// </summary>
        /// <param name="numpad">A numpad integer in the range 1–9; 5 and unrecognised values map to <see cref="EDirectionInput.None"/>.</param>
        /// <returns>The matching <see cref="EDirectionInput"/> value.</returns>
        public static EDirectionInput NumpadToInputType(int numpad)
        {
            return numpad switch
            {
                1 => EDirectionInput.Input1,
                2 => EDirectionInput.Input2,
                3 => EDirectionInput.Input3,
                4 => EDirectionInput.Input4,
                6 => EDirectionInput.Input6,
                7 => EDirectionInput.Input7,
                8 => EDirectionInput.Input8,
                9 => EDirectionInput.Input9,
                _ => EDirectionInput.None // covers 5 and any bad input
            };
        }
    }
}