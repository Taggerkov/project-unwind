using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.Audio.Contracts;

namespace Systems.Audio.Shared
{
    /// <summary>
    /// A single-track playback engine over <see cref="AudioManager"/>, scoped to one <see cref="AudioCategory"/>.
    /// Owns the live playback handle, the play/pause/resume/stop lifecycle, a category-wide <see cref="VolumeFader"/>,
    /// and a completion watcher that raises <see cref="Completed"/> only on the natural end of playback.
    /// </summary>
    /// <remarks>
    /// Higher-level systems (playlists, voiceline queues) compose this and supply their own "what plays next"
    /// policy by subscribing to <see cref="Completed"/>. The watcher's cancellation discipline — cancelling
    /// before any explicit stop, pause, or replacement so a stop never triggers a spurious completion — lives
    /// here, so it cannot be mis-mirrored by callers.
    /// </remarks>
    internal sealed class AudioStream : IDisposable
    {
        /// <summary>The audio playback surface used for all playback and volume operations.</summary>
        private readonly AudioManager _audioManager;

        /// <summary>The category this stream plays into. Drives bulk volume, speed, and stop on disposal.</summary>
        private readonly AudioCategory _category;

        /// <summary>Manages category-wide volume, including async fades.</summary>
        private readonly VolumeFader _fader;

        /// <summary>The <see cref="Guid"/> of the active playback, or <see cref="Guid.Empty"/> when idle or paused without a handle.</summary>
        private Guid _handle;

        /// <summary>Cancels the active <see cref="WatchAsync"/> task before any explicit stop, pause, or replacement.</summary>
        private CancellationTokenSource _watchCts;

        /// <summary>True while a track is actively playing; false when stopped or paused.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>True while a track is paused; false when playing, stopped, or idle.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>The category volume as last applied by this stream's fader.</summary>
        public float Volume => _fader.Volume;

        /// <summary>
        /// Raised when the current playback ends naturally (the clip finished).
        /// Never raised on explicit <see cref="Stop"/>, <see cref="Pause"/>, or replacement via <see cref="Play"/>.
        /// </summary>
        public event Action Completed;

        /// <summary>Constructs a stream bound to <paramref name="category"/>.</summary>
        /// <param name="audioManager">The audio playback surface.</param>
        /// <param name="category">The category this stream plays into.</param>
        public AudioStream(AudioManager audioManager, AudioCategory category)
        {
            _audioManager = audioManager;
            _category = category;
            _fader = new VolumeFader(audioManager);
        }

        /// <summary>
        /// Plays <paramref name="audioEvent"/>, replacing anything currently playing on this stream.
        /// Returns false if playback could not start (for example the clip was not preloaded); state is reset on failure.
        /// </summary>
        /// <param name="audioEvent">The event to play.</param>
        public bool Play(AudioEvent audioEvent)
        {
            Stop();

            _handle = _audioManager.Play(audioEvent);
            if (_handle == Guid.Empty)
            {
                IsPlaying = false;
                IsPaused = false;
                return false;
            }

            IsPlaying = true;
            IsPaused = false;
            _watchCts = new CancellationTokenSource();
            WatchAsync(_handle, _watchCts.Token).Forget();
            return true;
        }

        /// <summary>Stops playback immediately and resets state. No-op if nothing is playing or paused.</summary>
        public void Stop()
        {
            CancelAndDispose(ref _watchCts);
            if (_handle != Guid.Empty)
            {
                _audioManager.Stop(_handle);
                _handle = Guid.Empty;
            }
            IsPlaying = false;
            IsPaused = false;
        }

        /// <summary>Pauses the current playback, preserving position. No-op if not playing.</summary>
        public void Pause()
        {
            if (!IsPlaying || _handle == Guid.Empty) return;
            // Cancel the watcher before pausing: a paused source reports not-playing, which would
            // otherwise let the watcher fire a spurious Completed.
            CancelAndDispose(ref _watchCts);
            _audioManager.Pause(_handle);
            IsPlaying = false;
            IsPaused = true;
        }

        /// <summary>Resumes a paused track from its position. Returns false if not currently paused.</summary>
        public bool Resume()
        {
            if (!IsPaused || _handle == Guid.Empty) return false;
            _audioManager.Resume(_handle);
            IsPlaying = true;
            IsPaused = false;
            _watchCts = new CancellationTokenSource();
            WatchAsync(_handle, _watchCts.Token).Forget();
            return true;
        }

        /// <summary>Sets the master volume for this stream's category immediately. Cancels any in-flight fade.</summary>
        /// <param name="volume">Target volume. Clamped to a zero minimum.</param>
        public void SetVolume(float volume) => _fader.Set(_category, volume);

        /// <summary>Smoothly interpolates this stream's category volume to <paramref name="target"/> over <paramref name="duration"/> seconds.</summary>
        /// <param name="target">Target volume. Clamped to a zero minimum.</param>
        /// <param name="duration">Fade duration in seconds. Zero or negative applies immediately.</param>
        public UniTask FadeVolumeToAsync(float target, float duration) => _fader.FadeToAsync(_category, target, duration);

        /// <summary>Sets the master speed for this stream's category.</summary>
        /// <param name="speed">Target speed multiplier.</param>
        public void SetSpeed(float speed) => _audioManager.SetCategorySpeed(_category, speed);

        /// <summary>Cancels the watcher and any fade, stops all sounds in this category, and resets state.</summary>
        public void Dispose()
        {
            CancelAndDispose(ref _watchCts);
            _fader.Dispose();
            _audioManager.StopAll(_category);
            _handle = Guid.Empty;
            IsPlaying = false;
            IsPaused = false;
        }

        /// <summary>
        /// Awaits natural completion of <paramref name="handle"/>, then resets state and raises <see cref="Completed"/>.
        /// Returns silently when cancelled by an explicit stop, pause, or replacement.
        /// </summary>
        /// <param name="handle">The playback handle to watch.</param>
        /// <param name="ct">Token cancelled before any explicit stop, pause, or replacement.</param>
        private async UniTaskVoid WatchAsync(Guid handle, CancellationToken ct)
        {
            var cancelled = await _audioManager.AwaitCompletionAsync(handle, ct).SuppressCancellationThrow();
            if (cancelled) return;

            IsPlaying = false;
            _handle = Guid.Empty;
            CancelAndDispose(ref _watchCts);
            Completed?.Invoke();
        }

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