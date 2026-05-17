namespace Systems.Input
{
    public static class TickInputExtensions
    {
        public static bool HasButtons(this TickInput tick, EButtonInput buttons)
        {
            if (buttons == EButtonInput.None) return true;
            if (buttons.HasFlag(EButtonInput.Light) && !tick.LightAttack.Held) return false;
            if (buttons.HasFlag(EButtonInput.Medium) && !tick.MediumAttack.Held) return false;
            if (buttons.HasFlag(EButtonInput.Heavy) && !tick.HeavyAttack.Held) return false;
            if (buttons.HasFlag(EButtonInput.Unique) && !tick.UniqueAttack.Held) return false;
            if (buttons.HasFlag(EButtonInput.Guard) && !tick.GuardButton.Held) return false;
            if (buttons.HasFlag(EButtonInput.Ability) && !tick.AbilityButton.Held) return false;
            return true;
        }

        public static bool HasDirection(this TickInput tick, EDirectionInput direction)
            => tick.Direction.Current == direction;

        /// <summary>
        /// Mirrors the horizontal axis. Vertical directions (2, 5, 8) and None are unchanged.
        /// </summary>
        public static EDirectionInput FlipHorizontal(EDirectionInput d) => d switch
        {
            EDirectionInput.Input1 => EDirectionInput.Input3,
            EDirectionInput.Input3 => EDirectionInput.Input1,
            EDirectionInput.Input4 => EDirectionInput.Input6,
            EDirectionInput.Input6 => EDirectionInput.Input4,
            EDirectionInput.Input7 => EDirectionInput.Input9,
            EDirectionInput.Input9 => EDirectionInput.Input7,
            _ => d
        };

        /// <summary>
        /// Returns a copy of the frame with both Current and Previous directions
        /// mirrored. Previous must be flipped too — WasEntered/WasLeft compare both.
        /// </summary>
        public static TickInput WithFlippedHorizontal(this TickInput tick)
        {
            tick.Direction = new DirectionState
            {
                Current = FlipHorizontal(tick.Direction.Current),
                Previous = FlipHorizontal(tick.Direction.Previous)
            };
            return tick;
        }
    }
}