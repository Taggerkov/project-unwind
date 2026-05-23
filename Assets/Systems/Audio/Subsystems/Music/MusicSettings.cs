using System.Collections.Generic;
using UnityEngine;
using Systems.Audio.Shared;

namespace Systems.Audio.Music
{
    /// <summary>
    /// Configuration asset for the music management system.
    /// Defines separate playlists for menu and combat contexts.
    /// Create via right-click: Create → Unwind → Audio → Music Configuration.
    /// </summary>
    [CreateAssetMenu(fileName = "MusicSettings", menuName = "Unwind/Audio/Music Settings", order = 2)]
    public sealed class MusicSettings : ScriptableObject
    {
        /// <summary>Playlist used during MainMenu and CharacterSelect game states.</summary>
        [SerializeField]
        [Tooltip("Playlist used during MainMenu and CharacterSelect game states. All AudioEvents must have AudioCategory.Music.")]
        private AudioEvent[] menuPlaylist;

        /// <summary>Playlist used during Combat game state.</summary>
        [SerializeField]
        [Tooltip("Playlist used during Combat game state. All AudioEvents must have AudioCategory.Music.")]
        private AudioEvent[] combatPlaylist;

        /// <summary>Playlist used during MainMenu and CharacterSelect game states.</summary>
        public IReadOnlyList<AudioEvent> MenuPlaylist => menuPlaylist;

        /// <summary>Playlist used during Combat game state.</summary>
        public IReadOnlyList<AudioEvent> CombatPlaylist => combatPlaylist;

        /// <summary>
        /// Returns the playlist associated with <paramref name="type"/>, or null if the type is unrecognised.
        /// Adding a new <see cref="PlaylistType"/> requires only adding a field here and a case in this switch.
        /// </summary>
        /// <param name="type">The playlist type to look up.</param>
        public IReadOnlyList<AudioEvent> GetPlaylist(PlaylistType type) => type switch
        {
            PlaylistType.Menu => menuPlaylist,
            PlaylistType.Combat => combatPlaylist,
            _ => null
        };
    }
}
