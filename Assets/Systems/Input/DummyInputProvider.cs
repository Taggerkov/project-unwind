namespace Systems.Input
{
    /// <summary>
    /// Neutral <see cref="IInputProvider"/> that always returns numpad-5 (no direction, no buttons).
    /// Used to occupy a combatant slot without a player or CPU attached.
    /// </summary>
    public class DummyInputProvider : IInputProvider
    {
        /// <inheritdoc/>
        public EInputProviderType ProviderType => EInputProviderType.Dummy;

        /// <inheritdoc/>
        public InputBuffer Buffer { get; } = new();

        /// <summary>
        /// Writes a fully neutral <see cref="TickInput"/> to the buffer and returns it.
        /// </summary>
        /// <returns>A neutral tick with direction Input5 and all buttons in their default state.</returns>
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