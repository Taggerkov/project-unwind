using Systems.Combat.Core.Input;

namespace Systems.Input
{

    public enum EInputProviderType
    {
        Player,
        AI,
        Replay,
        NetworkBuffer
    }

    public struct ButtonState
    {
        public bool Pressed; // True only on the first frame of the press
        public bool Held; // True every frame the button is down
        public bool Released; // True only on the first frame of the release
    }
    public struct TickInput
    {
        public EInputType Direction; // 1-9
        public ButtonState LightAttack;
        public ButtonState MediumAttack;
        public ButtonState HeavyAttack;
        public ButtonState UniqueAttack;
        public ButtonState GuardButton;
        public ButtonState AbilityButton;

        public bool IsNeutral => Direction == EInputType.Input5 && !LightAttack.Held && !MediumAttack.Held &&
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
    }
}
