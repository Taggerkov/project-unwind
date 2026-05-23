using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.Audio.Contracts;
using Systems.Audio.Shared;
using UnityEngine;

namespace Systems.Audio.Music
{
    /// <summary>
    /// Handles the Addressables lifecycle for music playlists: validation, parallel preload, and unload.
    /// All Music-specific validation rules live here.
    /// </summary>
    internal sealed class PlaylistLoader
    {
        /// <summary>The audio playback surface used for preload and unload operations.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>Constructs the loader.</summary>
        /// <param name="audioManager">The audio playback surface used for preload and unload operations.</param>
        internal PlaylistLoader(AudioManager audioManager) => _audioManager = audioManager;

        /// <summary>
        /// Filters <paramref name="playlist"/> to entries that are safe to preload and play.
        /// Logs a warning or error for each skipped entry. Returns an empty list if the input is null or empty.
        /// </summary>
        /// <param name="playlist">The raw playlist to validate.</param>
        /// <returns>A new list containing only valid entries.</returns>
        internal List<AudioEvent> Validate(IReadOnlyList<AudioEvent> playlist)
        {
            var valid = new List<AudioEvent>();

            if (playlist == null || playlist.Count == 0)
            {
                AudioDiagnostics.Warn("Playlist is null or empty.");
                return valid;
            }

            for (var i = 0; i < playlist.Count; i++)
            {
                var ev = playlist[i];

                if (ev == null)
                {
                    AudioDiagnostics.Error($"Null AudioEvent at index {i}. Skipping.");
                    continue;
                }

                if (ev.Category != AudioCategory.Music)
                {
                    AudioDiagnostics.Warn($"'{ev.name}' at index {i} is not AudioCategory.Music. Skipping.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ev.Key))
                {
                    AudioDiagnostics.Error($"'{ev.name}' at index {i} has no Addressable reference. Skipping.");
                    continue;
                }

                if (ev.Loop)
                    AudioDiagnostics.Warn($"'{ev.name}' at index {i} is looping — playlist will not auto-advance.");

                valid.Add(ev);
            }

            return valid;
        }

        /// <summary>
        /// Preloads all entries in parallel. Failed or cancelled entries are excluded from the result.
        /// Never throws — callers check the returned count.
        /// </summary>
        /// <param name="playlist">The validated list of events to preload.</param>
        /// <param name="ct">Token to cancel all pending load operations.</param>
        /// <returns>A list of successfully loaded events. May be shorter than <paramref name="playlist"/> if entries failed.</returns>
        internal async UniTask<List<AudioEvent>> PreloadAsync(List<AudioEvent> playlist, CancellationToken ct)
        {
            var tasks = new UniTask<AudioEvent>[playlist.Count];
            for (var i = 0; i < playlist.Count; i++)
                tasks[i] = PreloadSingleAsync(playlist[i], ct);

            var results = await UniTask.WhenAll(tasks);

            var loaded = new List<AudioEvent>(results.Length);
            foreach (var ev in results)
                if (ev != null) loaded.Add(ev);
            return loaded;
        }

        /// <summary>Releases all clips in <paramref name="playlist"/> from memory. No-op if <paramref name="playlist"/> is null.</summary>
        /// <param name="playlist">The playlist whose clips to release.</param>
        internal void Unload(AudioEvent[] playlist)
        {
            if (playlist == null) return;
            foreach (var ev in playlist)
                _audioManager.Unload(ev);
        }

        /// <summary>Preloads a single entry. Returns null on cancellation or load failure so <see cref="UniTask.WhenAll"/> never throws.</summary>
        /// <param name="ev">The event to preload.</param>
        /// <param name="ct">Token to cancel the load.</param>
        /// <returns>The event if successfully loaded; null otherwise.</returns>
        private async UniTask<AudioEvent> PreloadSingleAsync(AudioEvent ev, CancellationToken ct)
        {
            try
            {
                await _audioManager.PreloadAsync(ev, ct);
                return ev;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                Debug.LogError($"[MusicManager] Failed to preload '{ev.name}': {ex.Message}");
                return null;
            }
        }
    }
}