namespace Systems.Audio.Contracts
{
    /// <summary>
    /// Classifies an audio request by its logical role within the game.
    /// Used to route playback and apply per-category volume controls.
    /// </summary>
    public enum AudioCategory
    {
        /// <summary>Background music tracks.</summary>
        Music,

        /// <summary>Short gameplay sound effects: hits, footsteps, UI feedback.</summary>
        Sfx,

        /// <summary>Continuous environmental sounds: wind, crowd, room tone.</summary>
        Ambient,

        /// <summary>Character dialogue and voiced lines.</summary>
        Voice
    }
}