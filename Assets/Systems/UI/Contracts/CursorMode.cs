namespace Systems.UI.Contracts
{
    /// <summary>
    /// How a screen's cursor behaves, so <see cref="UIManager"/> can drive the right model.
    /// </summary>
    public enum CursorMode
    {
        /// <summary>
        /// One identity-agnostic cursor shared by every controller; the selection is mirrored onto
        /// every controller's event system. No per-player cursor objects (e.g. the main menu).
        /// </summary>
        Shared,

        /// <summary>
        /// One cursor per player, with a shared cursor shown when two players rest on the same
        /// selectable. Identity matters (e.g. character select, where player 0 and 1 pick separately).
        /// </summary>
        PerPlayer
    }
}
