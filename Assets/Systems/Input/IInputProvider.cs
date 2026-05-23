using System;

namespace Systems.Input
{
    public enum EInputProviderType
    {
        Player,
        Cpu,
        Dummy,
        Replay,
        NetworkBuffer,
    }

    public struct ButtonState
    {
        public bool Pressed; // True only on the first frame of the press
        public bool Held; // True every frame the button is down
        public bool Released; // True only on the first frame of the release
    }

    public struct DirectionState : IEquatable<DirectionState>
    {
        public EDirectionInput Current;
        public EDirectionInput Previous;
        
        public DirectionState(EDirectionInput current, EDirectionInput previous)
        {
            Current = current;
            Previous = previous;
        }

        public bool WasEntered(EDirectionInput dir) => Current == dir && Previous != dir;
        public bool IsHeld(EDirectionInput dir) => Current == dir;
        public bool WasLeft(EDirectionInput dir) => Previous == dir && Current != dir;

        public static bool operator ==(DirectionState left, DirectionState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DirectionState left, DirectionState right)
        {
            return !left.Equals(right);
        }

        public bool Equals(DirectionState other)
        {
            return Current == other.Current;
        }

        public override bool Equals(object obj)
        {
            return obj is DirectionState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Current;
        }
    }

    public struct TickInput
    {
        public DirectionState Direction; // 1-9
        public ButtonState LightAttack;
        public ButtonState MediumAttack;
        public ButtonState HeavyAttack;
        public ButtonState UniqueAttack;
        public ButtonState GuardButton;
        public ButtonState AbilityButton;

        public bool IsNeutral => Direction.Current == EDirectionInput.None && !LightAttack.Held &&
                                 !MediumAttack.Held &&
                                 !HeavyAttack.Held &&
                                 !UniqueAttack.Held && !GuardButton.Held && !AbilityButton.Held;
    }

    public interface IInputProvider
    {
        public EInputProviderType ProviderType { get; }
        public InputBuffer Buffer { get; }

        /// <summary>
        /// Called each tick to know what a specific combatant is trying to do.
        /// </summary>
        /// <returns>The FrameInput with this current tick's information.</returns>
        public TickInput UpdateFrameInput();

        /// <summary>
        /// Drops any input latched before combat (e.g. menu/Help presses) so the first
        /// tick starts clean. No-op for providers without buffered physical input.
        /// </summary>
        public void Flush() { }
    }
}