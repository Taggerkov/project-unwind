namespace Systems.Audio.Contracts
{
    /// <summary>
    /// Classifies an audio request by its logical role within the game.
    /// Used to route playback and apply per-category volume controls.
    /// </summary>
    public enum AudioCategory
    {
        Music,
        Sfx,
        Ambient,
        Voice
    }
}