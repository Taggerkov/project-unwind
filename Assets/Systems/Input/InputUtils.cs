using Systems.Combat.Core.Input;
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
                // If the direction or ANY button state differs, it's a new input
                return TickData.Direction == other.Direction &&
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

            if (x == 0 && y == 0) return 5; // Neutral
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
        
        public static EInputType NumpadToInputType(int numpad)
        {
            return numpad switch
            {
                1 => EInputType.Input1,
                2 => EInputType.Input2,
                3 => EInputType.Input3,
                4 => EInputType.Input4,
                5 => EInputType.Input5,
                6 => EInputType.Input6,
                7 => EInputType.Input7,
                8 => EInputType.Input8,
                9 => EInputType.Input9,
                _ => EInputType.Input5 // Default to neutral if something goes wrong
            };
        }
    }
}