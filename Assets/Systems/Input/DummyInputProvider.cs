namespace Systems.Input
{
    public class DummyInputProvider : IInputProvider
    {
        public EInputProviderType ProviderType => EInputProviderType.Dummy;
        public InputBuffer Buffer { get; } = new();

        public TickInput UpdateFrameInput()
        {
            TickInput input = new TickInput
            {
                Direction = new DirectionState
                {
                    Current = EDirectionInput.Input5,
                    Previous = EDirectionInput.Input5
                },
                LightAttack = default,
                MediumAttack = default,
                HeavyAttack = default,
                UniqueAttack = default,
                GuardButton = default,
                AbilityButton = default
            };
            Buffer.Write(input);
            return input;
        }
    }
}