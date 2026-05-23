namespace Systems.Audio.Music
{
    /// <summary>
    /// Identifies which music playlist is currently active.
    /// </summary>
    public enum PlaylistType
    {
        /// <summary>No playlist is active.</summary>
        None,

        /// <summary>Menu playlist; active during the MainMenu and CharacterSelect game states.</summary>
        Menu,

        /// <summary>Combat playlist; active during the Combat game state.</summary>
        Combat
    }
}