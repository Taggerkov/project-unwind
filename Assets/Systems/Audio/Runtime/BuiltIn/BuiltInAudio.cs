using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Systems.Audio.Contracts;
using Systems.Audio.Runtime.BuiltIn.Internal;
using UnityEngine;

namespace Systems.Audio.Runtime.BuiltIn
{
    /// <summary>
    /// BuiltIn implementation of <see cref="IAudioService"/>.
    /// Owns and orchestrates <see cref="UnityAudioBank"/> and <see cref="UnityAudioPool"/>.
    /// Registered as <see cref="IAudioService"/> in the container.
    /// </summary>
    internal sealed class BuiltInAudio : IAudioService, ICategoryProvider
    {
        /// <summary>The preloaded clip bank, keyed by Addressables address string.</summary>
        private readonly UnityAudioBank _bank;

        /// <summary>The <see cref="AudioSource"/> pool and coroutine host.</summary>
        private readonly UnityAudioPool _pool;

        /// <summary>Active handles per category, used for bulk operations and cleanup.</summary>
        private readonly Dictionary<AudioCategory, List<AudioHandle>> _activeHandles = new();

        /// <summary>Master volume multiplier per category, applied at playtime and propagated live.</summary>
        private readonly Dictionary<AudioCategory, float> _categoryVolumes = new();

        /// <summary>Master speed multiplier per category, applied at playtime and propagated live.</summary>
        private readonly Dictionary<AudioCategory, float> _categorySpeeds = new();

        /// <summary>
        /// Constructs the BuiltIn backend, initialising the clip bank, source pool, and per-category tracking.
        /// </summary>
        /// <param name="settings">Pool and backend configuration.</param>
        public BuiltInAudio(AudioSettings settings)
        {
            _bank = new UnityAudioBank();
            _pool = new UnityAudioPool(settings.PoolSize);

            foreach (AudioCategory category in Enum.GetValues(typeof(AudioCategory)))
            {
                _activeHandles[category] = new List<AudioHandle>();
                _categoryVolumes[category] = 1f;
                _categorySpeeds[category] = 1f;
            }
        }

        /// <inheritdoc/>
        public async Task PreloadAsync(string key, CancellationToken cancellationToken = default)
            => await _bank.PreloadAsync(key, cancellationToken);

        /// <inheritdoc/>
        public void Unload(string key) => _bank.Unload(key);

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="request"/> key was not preloaded via <see cref="PreloadAsync"/>.
        /// </exception>
        public IAudioHandle Play(in AudioRequest request, Guid uuid)
        {
            if (!_bank.TryGet(request.Key, out var clip))
                throw new InvalidOperationException(
                    $"[BuiltInAudio] Clip '{request.Key}' was not preloaded. Call PreloadAsync before Play.");

            var source = _pool.Rent();
            source.clip = clip;
            source.loop = request.Loop;
            source.Play();

            var handle = new AudioHandle(uuid, source, _pool, clip, request.Category, request.Volume, request.Speed,
                this, OnHandleStopped);
            handle.ApplyVolume();
            handle.ApplySpeed();

            _activeHandles[request.Category].Add(handle);
            return handle;
        }

        /// <inheritdoc/>
        public void PauseAll(AudioCategory category)
        {
            foreach (var handle in _activeHandles[category]) handle.Pause();
        }

        /// <inheritdoc/>
        public void ResumeAll(AudioCategory category)
        {
            foreach (var handle in _activeHandles[category]) handle.Resume();
        }

        /// <inheritdoc/>
        public void StopAll(AudioCategory category)
        {
            var handles = _activeHandles[category];
            for (var i = handles.Count - 1; i >= 0; i--) handles[i].Stop();
        }

        /// <inheritdoc/>
        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            _categoryVolumes[category] = volume;
            foreach (var handle in _activeHandles[category]) handle.ApplyVolume();
        }

        /// <inheritdoc/>
        public float GetCategoryVolume(AudioCategory category) => _categoryVolumes[category];

        /// <inheritdoc/>
        public void SetCategorySpeed(AudioCategory category, float speed)
        {
            _categorySpeeds[category] = speed;
            foreach (var handle in _activeHandles[category]) handle.ApplySpeed();
        }

        /// <inheritdoc/>
        public float GetCategorySpeed(AudioCategory category) => _categorySpeeds[category];

        /// <summary>
        /// Callback invoked by <see cref="AudioHandle"/> on release.
        /// Removes the handle from active tracking.
        /// </summary>
        private void OnHandleStopped(AudioHandle handle)
        {
            foreach (var list in _activeHandles.Values) list.Remove(handle);
        }
    }
}