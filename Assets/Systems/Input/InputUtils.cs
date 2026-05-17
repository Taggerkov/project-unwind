using UnityEngine;

namespace Systems.Input
{
    public static class InputUtils
    {
        public class CompressedInput
        {
            public TickInput TickData;
            public int FrameCount;

            public CompressedInput(TickInput tick)
            {
                TickData = tick;
                FrameCount = 1;
            }

            public bool Matches(TickInput other)
            {
                // if (TickData.Direction.Current != other.Direction.Current)
                // {
                //     Debug.Log($"Direction changed from {TickData.Direction.Current} to {other.Direction.Current}");
                // }

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