using System;
using Systems.Audio.Shared;

namespace Systems.Audio.Music
{
    /// <summary>
    /// Owns playlist state: current index, shuffle mode, and sequential/random advancement.
    /// Purely in-memory — no audio calls, no async.
    /// </summary>
    internal sealed class PlaylistSequencer
    {
        /// <summary>Returns the <see cref="AudioEvent"/> at the current index, or null if no playlist is loaded.</summary>
        internal AudioEvent Current =>
            Playlist != null && CurrentIndex >= 0 && CurrentIndex < Playlist.Length
                ? Playlist[CurrentIndex]
                : null;

        /// <summary>Returns the zero-based index of the current track within the active playlist.</summary>
        internal int CurrentIndex { get; private set; }

        /// <summary>Returns the number of tracks in the active playlist, or zero if none is loaded.</summary>
        internal int Count => Playlist?.Length ?? 0;
        /// <summary>Returns true when a non-empty playlist is loaded.</summary>
        internal bool HasPlaylist => Playlist is { Length: > 0 };

        /// <summary>When true, <see cref="Advance"/> picks a random track instead of the sequential next one.</summary>
        internal bool ShuffleEnabled { get; private set; }

        /// <summary>Exposes the raw array so PlaylistLoader can unload it on playlist change.</summary>
        internal AudioEvent[] Playlist { get; private set; }

        /// <summary>Loads <paramref name="playlist"/> and resets the index to zero.</summary>
        /// <param name="playlist">The playlist to load.</param>
        internal void Load(AudioEvent[] playlist)
        {
            Playlist = playlist;
            CurrentIndex = 0;
        }

        /// <summary>Clears the loaded playlist.</summary>
        internal void Clear() => Playlist = null;

        /// <summary>Advances to the next track. Shuffles if enabled; otherwise wraps sequentially. No-op when no playlist is loaded.</summary>
        internal void Advance()
        {
            if (!HasPlaylist) return;
            CurrentIndex = ShuffleEnabled && Playlist.Length > 1
                ? GetShuffledIndex()
                : (CurrentIndex + 1) % Playlist.Length;
        }

        /// <summary>Goes to the previous track, wrapping to the last from index 0. Always sequential. No-op when no playlist is loaded.</summary>
        internal void Previous()
        {
            if (!HasPlaylist) return;
            CurrentIndex = (CurrentIndex - 1 + Playlist.Length) % Playlist.Length;
        }

        /// <summary>Returns false if <paramref name="index"/> is out of range.</summary>
        internal bool GoTo(int index)
        {
            if (!HasPlaylist || index < 0 || index >= Playlist.Length) return false;
            CurrentIndex = index;
            return true;
        }

        /// <summary>Returns false if <paramref name="audioEvent"/> is not in the loaded playlist.</summary>
        internal bool GoTo(AudioEvent audioEvent)
        {
            if (!HasPlaylist || audioEvent == null) return false;
            var index = Array.IndexOf(Playlist, audioEvent);
            if (index < 0) return false;
            CurrentIndex = index;
            return true;
        }

        /// <summary>Enables or disables shuffle mode. Takes effect on the next <see cref="Advance"/> call.</summary>
        /// <param name="enabled">True to enable shuffle; false for sequential order.</param>
        internal void SetShuffle(bool enabled) => ShuffleEnabled = enabled;

        /// <summary>Returns a random index that is never equal to the current one. Maps [0, length-2] then shifts past the excluded index for uniform distribution.</summary>
        private int GetShuffledIndex()
        {
            var next = UnityEngine.Random.Range(0, Playlist.Length - 1);
            if (next >= CurrentIndex) next++;
            return next;
        }
    }
}