using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Systems.Audio.Shared;

namespace Systems.Audio.Music
{
    /// <summary>
    /// Coordinates playlist-based music playback.
    /// Delegates single-track playback lifecycle to <see cref="AudioStream"/>, asset lifecycle to
    /// <see cref="PlaylistLoader"/>, and track sequencing to <see cref="PlaylistSequencer"/>.
    /// Owns only playlist activation flow and the auto-advance policy.
    /// </summary>
    public sealed class MusicManager : IDisposable
    {
        /// <summary>The single-track playback engine, scoped to <see cref="AudioCategory.Music"/>.</summary>
        private readonly AudioStream _stream;

        /// <summary>The settings asset containing the menu and combat playlist arrays.</summary>
        private readonly MusicSettings _settings;

        /// <summary>Owns track index and shuffle state for the active playlist.</summary>
        private readonly PlaylistSequencer _sequencer = new();

        /// <summary>Handles Addressables lifecycle: validation, preload, and unload.</summary>
        private readonly PlaylistLoader _loader;

        /// <summary>The <see cref="PlaylistType"/> that is currently loaded and playing.</summary>
        private PlaylistType _activePlaylistType;

        /// <summary>Cancels any in-flight <see cref="ActivatePlaylist"/> operation when a new activation begins.</summary>
        private CancellationTokenSource _activationCts;

        /// <summary>
        /// Raised when the current track changes, either by auto-advance or manual control.
        /// Provides the <see cref="AudioEvent"/> now playing.
        /// </summary>
        public event Action<AudioEvent> OnTrackChanged;

        /// <summary>
        /// Raised when the active playlist type changes — including when music stops (<see cref="PlaylistType.None"/>).
        /// For non-None activations, fires after preloading completes and the first track starts.
        /// </summary>
        public event Action<PlaylistType> OnPlaylistChanged;

        /// <summary>Returns true if a track is actively playing (not paused, not stopped).</summary>
        public bool IsPlaying => _stream.IsPlaying;

        /// <summary>Returns true if playback is currently paused.</summary>
        public bool IsPaused => _stream.IsPaused;

        /// <summary>Returns the AudioEvent currently playing or paused. Null if no playlist is active.</summary>
        public AudioEvent CurrentTrack => _sequencer.Current;

        /// <summary>Returns the zero-based index of the current track within the active playlist.</summary>
        public int CurrentTrackIndex => _sequencer.CurrentIndex;

        /// <summary>Returns which playlist is currently active.</summary>
        public PlaylistType ActivePlaylist => _activePlaylistType;

        /// <summary>Returns true if shuffle mode is enabled.</summary>
        public bool ShuffleEnabled => _sequencer.ShuffleEnabled;

        /// <summary>Returns the number of tracks in the currently active playlist.</summary>
        public int TrackCount => _sequencer.Count;

        /// <summary>Returns the current master volume for all music.</summary>
        public float Volume => _stream.Volume;

        /// <summary>Constructs the manager. Called by Reflex via constructor injection.</summary>
        public MusicManager(AudioManager audioManager, MusicSettings settings)
        {
            _settings = settings;
            _loader = new PlaylistLoader(audioManager);
            _stream = new AudioStream(audioManager, Contracts.AudioCategory.Music);
            _stream.Completed += OnTrackCompleted;
        }

        /// <summary>Stops active playback and unloads all preloaded assets.</summary>
        public void Dispose()
        {
            CancelAndDispose(ref _activationCts);
            _stream.Completed -= OnTrackCompleted;
            _stream.Dispose();
            _loader.Unload(_sequencer.Playlist);
            _sequencer.Clear();
            _activePlaylistType = PlaylistType.None;
        }

        // ── Playlist Activation ─────────────────────────────────────────────

        /// <summary>
        /// Stops current playback, unloads the active playlist, and activates <paramref name="playlistType"/>.
        /// Passing <see cref="PlaylistType.None"/> stops music without loading a new playlist.
        /// Cancels any in-flight activation so rapid calls do not corrupt playlist state.
        /// </summary>
        public async UniTask ActivatePlaylist(PlaylistType playlistType)
        {
            if (_activePlaylistType == playlistType && _stream.IsPlaying)
            {
                CancelAndDispose(ref _activationCts);
                return;
            }

            CancelAndDispose(ref _activationCts);
            _activationCts = new CancellationTokenSource();
            var ct = _activationCts.Token;

            _stream.Stop();
            _loader.Unload(_sequencer.Playlist);
            _sequencer.Clear();
            _activePlaylistType = PlaylistType.None;

            if (playlistType == PlaylistType.None)
            {
                OnPlaylistChanged?.Invoke(PlaylistType.None);
                return;
            }

            var targetPlaylist = _settings.GetPlaylist(playlistType);
            if (targetPlaylist == null)
            {
                AudioDiagnostics.Warn($"No playlist configured for {playlistType}.");
                return;
            }

            var valid = _loader.Validate(targetPlaylist);
            if (valid.Count == 0)
            {
                AudioDiagnostics.Warn($"No valid tracks in {playlistType} playlist.");
                return;
            }

            var preloaded = await _loader.PreloadAsync(valid, ct);
            if (ct.IsCancellationRequested) return;

            if (preloaded.Count == 0)
            {
                Debug.LogError($"[MusicManager] Failed to preload any tracks for {playlistType} playlist.");
                return;
            }

            _sequencer.Load(preloaded.ToArray());
            _activePlaylistType = playlistType;
            PlayCurrentTrack();
            OnPlaylistChanged?.Invoke(playlistType);
        }

        // ── Playback ────────────────────────────────────────────────────────

        /// <summary>Plays the track at the current sequencer index. Replaces any track already playing.</summary>
        private void PlayCurrentTrack()
        {
            if (!_sequencer.HasPlaylist) return;

            var ev = _sequencer.Current;
            if (!_stream.Play(ev))
            {
                AudioDiagnostics.Error($"Failed to play '{ev?.name}'.");
                return;
            }

            OnTrackChanged?.Invoke(ev);
        }

        /// <summary>Auto-advance policy: when a track finishes naturally, advance and play the next one.</summary>
        private void OnTrackCompleted()
        {
            _sequencer.Advance();
            PlayCurrentTrack();
        }

        // ── Public Control ──────────────────────────────────────────────────

        /// <summary>
        /// Stops music playback immediately. The loaded playlist is retained and
        /// playback can resume from the current track via <see cref="Resume"/>.
        /// </summary>
        public void Stop() => _stream.Stop();

        /// <summary>Skips to the next track. Uses shuffle if enabled; otherwise wraps sequentially.</summary>
        public void NextTrack()
        {
            if (!_sequencer.HasPlaylist)
            {
                AudioDiagnostics.Warn("Cannot skip: no active playlist.");
                return;
            }

            _sequencer.Advance();
            PlayCurrentTrack();
        }

        /// <summary>Goes back to the previous track, wrapping to the last track from index 0. Always sequential.</summary>
        public void PreviousTrack()
        {
            if (!_sequencer.HasPlaylist)
            {
                AudioDiagnostics.Warn("Cannot go to previous: no active playlist.");
                return;
            }

            _sequencer.Previous();
            PlayCurrentTrack();
        }

        /// <summary>Pauses the currently playing track, preserving playback position.</summary>
        public void Pause() => _stream.Pause();

        /// <summary>Resumes a paused track. If nothing is active but a playlist is loaded, begins from the current index.</summary>
        public void Resume()
        {
            if (_stream.IsPaused)
            {
                _stream.Resume();
                return;
            }

            if (!_stream.IsPlaying && _sequencer.HasPlaylist) PlayCurrentTrack();
        }

        /// <summary>Pauses if playing; resumes if paused or stopped.</summary>
        public void TogglePause()
        {
            if (_stream.IsPlaying) Pause();
            else Resume();
        }

        /// <summary>Restarts the current track from the beginning.</summary>
        public void Restart()
        {
            if (!_sequencer.HasPlaylist)
            {
                AudioDiagnostics.Warn("Cannot restart: no active playlist.");
                return;
            }

            PlayCurrentTrack();
        }

        /// <summary>Plays a specific track by reference. Must exist in the currently active playlist.</summary>
        public void PlayTrack(AudioEvent audioEvent)
        {
            if (audioEvent == null)
            {
                AudioDiagnostics.Error("Cannot play: AudioEvent is null.");
                return;
            }

            if (!_sequencer.HasPlaylist)
            {
                AudioDiagnostics.Warn("Cannot play: no active playlist.");
                return;
            }

            if (!_sequencer.GoTo(audioEvent))
            {
                AudioDiagnostics.Warn($"'{audioEvent.name}' not found in active playlist.");
                return;
            }

            PlayCurrentTrack();
        }

        /// <summary>Plays the track at the given zero-based index within the active playlist.</summary>
        public void PlayByIndex(int index)
        {
            if (!_sequencer.HasPlaylist)
            {
                AudioDiagnostics.Warn("Cannot play: no active playlist.");
                return;
            }

            if (!_sequencer.GoTo(index))
            {
                AudioDiagnostics.Warn($"Index {index} is out of range (0–{_sequencer.Count - 1}).");
                return;
            }

            PlayCurrentTrack();
        }

        // ── Volume / Speed / Shuffle ────────────────────────────────────────

        /// <summary>Sets the master volume for all music immediately. Cancels any in-flight fade.</summary>
        public void SetVolume(float volume) => _stream.SetVolume(volume);

        /// <summary>Smoothly interpolates the music volume to <paramref name="target"/> over <paramref name="duration"/> seconds.</summary>
        public UniTask FadeVolumeToAsync(float target, float duration) => _stream.FadeVolumeToAsync(target, duration);

        /// <summary>Sets the master speed for all music playbacks.</summary>
        public void SetSpeed(float speed) => _stream.SetSpeed(speed);

        /// <summary>Enables or disables shuffle mode.</summary>
        public void SetShuffle(bool enabled) => _sequencer.SetShuffle(enabled);

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>Cancels and disposes <paramref name="cts"/>, then sets it to null. Safe to call when already null.</summary>
        /// <param name="cts">The token source to tear down.</param>
        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
}