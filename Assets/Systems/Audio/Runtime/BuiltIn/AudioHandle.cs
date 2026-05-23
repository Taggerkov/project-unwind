using System;
using System.Collections;
using Systems.Audio.Contracts;
using Systems.Audio.Runtime.BuiltIn.Internal;
using UnityEngine;

namespace Systems.Audio.Runtime.BuiltIn
{
    /// <summary>
    /// Wraps a rented <see cref="AudioSource"/> returned by <see cref="BuiltInAudio.Play"/>.
    /// Manages playback lifecycle, volume, speed, pause, and resume for a single audio instance.
    /// One-shot clips schedule their own cleanup via coroutine.
    /// Looping clips must be stopped explicitly by the calling system.
    /// </summary>
    internal sealed class AudioHandle : IAudioHandle
    {
        /// <summary>The handle's own volume layer. Combined with the category multiplier at writing time.</summary>
        private float _volume;

        /// <summary>The handle's own speed layer. Combined with the category multiplier at writing time.</summary>
        private float _speed;

        /// <summary>The category this handle belongs to. Used to query the current category multipliers.</summary>
        private readonly AudioCategory _category;

        /// <summary>Provides current category multipliers for volume and speed calculations.</summary>
        private readonly ICategoryProvider _audio;

        /// <summary>The rented <see cref="AudioSource"/> driving playback. Nulled on release.</summary>
        private AudioSource _source;

        /// <summary>The pool to return the <see cref="AudioSource"/> to on release.</summary>
        private readonly UnityAudioPool _pool;

        /// <summary>The clip being played. Retained for duration calculations in the cleanup coroutine.</summary>
        private readonly AudioClip _clip;

        /// <summary>Whether the clip loops. Looping handles have no cleanup coroutine and require explicit stopping.</summary>
        private readonly bool _isLooping;

        /// <summary>Callback invoked on release.</summary>
        private readonly Action<AudioHandle> _onStopped;

        /// <summary>The active cleanup coroutine. Null when not running.</summary>
        private Coroutine _cleanupCoroutine;

        /// <summary>Whether playback is currently paused.</summary>
        private bool _isPaused;

        /// <inheritdoc/>
        public Guid Uuid { get; }

        /// <inheritdoc/>
        public bool IsPlaying => _source != null && _source.isPlaying;

        /// <inheritdoc/>
        public bool IsPaused => _isPaused;

        /// <inheritdoc/>
        public float Volume => _volume;

        /// <inheritdoc/>
        public float Speed => _speed;

        /// <inheritdoc/>
        public bool IsLooping => _isLooping;

        /// <inheritdoc/>
        public float Time => _source != null ? _source.time : 0f;

        /// <inheritdoc/>
        public float Length => _clip != null ? _clip.length : 0f;

        /// <inheritdoc/>
        public event Action<Guid> OnReleased;

        /// <summary>
        /// BuiltIn playback handle wrapping a rented <see cref="AudioSource"/>.
        /// Owns the cleanup coroutine for one-shot clips and delegates release back to <see cref="BuiltInAudio"/>.
        /// </summary>
        /// <param name="uuid">The <see cref="Guid"/> assigned by <see cref="AudioManager"/> at playtime.</param>
        /// <param name="source">The rented <see cref="AudioSource"/> to wrap.</param>
        /// <param name="pool">The pool to return the source to on release.</param>
        /// <param name="clip">The clip being played. Retained for duration calculations.</param>
        /// <param name="category">The category this handle belongs to.</param>
        /// <param name="volume">The initial handle volume layer.</param>
        /// <param name="speed">The initial handle speed layer.</param>
        /// <param name="audio">Provides category multipliers for volume and speed calculations.</param>
        /// <param name="onStopped">Invoked on release. Used by <see cref="BuiltInAudio"/> to remove this handle from active tracking.</param>
        public AudioHandle(
            Guid uuid,
            AudioSource source,
            UnityAudioPool pool,
            AudioClip clip,
            AudioCategory category,
            float volume,
            float speed,
            ICategoryProvider audio,
            Action<AudioHandle> onStopped)
        {
            Uuid = uuid;
            _source = source;
            _pool = pool;
            _clip = clip;
            _category = category;
            _volume = volume;
            _speed = speed;
            _audio = audio;
            _isLooping = source.loop;
            _onStopped = onStopped;

            if (!_isLooping) _cleanupCoroutine = _pool.StartCoroutine(CleanupRoutine());
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (_source == null) return;
            CancelCleanupCoroutine();
            _source.Stop();
            Release();
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (_source == null || !_source.isPlaying) return;
            _source.Pause();
            _isPaused = true;
            CancelCleanupCoroutine();
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (_source == null || !_isPaused) return;
            _source.UnPause();
            _isPaused = false;
            RestartCleanupCoroutine();
        }

        /// <inheritdoc/>
        /// <remarks>Also used by <see cref="BuiltInAudio"/> for O(1) removal on release.</remarks>
        public AudioCategory Category => _category;

        /// <inheritdoc/>
        public void SetVolume(float volume)
        {
            if (_source == null) return;
            _volume = Mathf.Max(0f, volume);
            ApplyVolume();
        }

        /// <inheritdoc/>
        public void SetSpeed(float speed)
        {
            if (_source == null) return;
            _speed = speed;
            ApplySpeed();
        }

        /// <summary>
        /// Writes the combined handle and category volume to source.
        /// The single writing point for the source volume.
        /// </summary>
        internal void ApplyVolume()
        {
            if (_source == null) return;
            _source.volume = Mathf.Clamp01(_volume * _audio.GetCategoryVolume(_category));
        }

        /// <summary>
        /// Writes the combined handle and category speed to source pitch.
        /// Reschedules the cleanup coroutine if playing and not looping.
        /// The single writing point for source pitch.
        /// </summary>
        internal void ApplySpeed()
        {
            if (_source == null) return;
            _source.pitch = _speed * _audio.GetCategorySpeed(_category);
            if (!_isPaused && !_isLooping) RestartCleanupCoroutine();
        }

        /// <summary>
        /// Cancels the active cleanup coroutine and starts a new one based on the current playback state.
        /// Called on construction for one-shot clips, on <see cref="SetSpeed"/> while playing,
        /// and on <see cref="Resume"/> to account for any speed changes accumulated during pause.
        /// </summary>
        private void RestartCleanupCoroutine()
        {
            CancelCleanupCoroutine();
            if (_source == null || _isLooping) return;
            _cleanupCoroutine = _pool.StartCoroutine(CleanupRoutine());
        }

        /// <summary>
        /// Stops the active cleanup coroutine if one is running.
        /// </summary>
        private void CancelCleanupCoroutine()
        {
            if (_cleanupCoroutine == null) return;
            _pool.StopCoroutine(_cleanupCoroutine);
            _cleanupCoroutine = null;
        }

        /// <summary>
        /// Waits for the remaining clip duration at the current speed, then releases the handle.
        /// </summary>
        private IEnumerator CleanupRoutine()
        {
            var speedProduct = _speed * _audio.GetCategorySpeed(_category);
            if (speedProduct <= 0f) { Release(); yield break; }
            var remaining = (_clip.length - _source.time) / speedProduct;
            yield return new WaitForSecondsRealtime(remaining);
            Release();
        }

        /// <summary>
        /// Returns the rented <see cref="AudioSource"/> to the pool and raises <see cref="OnReleased"/>.
        /// Called by <see cref="Stop"/> and by <see cref="CleanupRoutine"/> on natural completion.
        /// </summary>
        private void Release()
        {
            _onStopped?.Invoke(this);
            OnReleased?.Invoke(Uuid);
            _pool.Return(_source);
            _source = null;
        }
    }
}