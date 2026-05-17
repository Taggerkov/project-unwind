using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Systems.Audio.Runtime.BuiltIn.Internal
{
    /// <summary>
    /// Resolves string keys to <see cref="AudioClip"/> assets via Addressables.
    /// Owns the preload cache and all Addressables handle lifetimes.
    /// Deduplicates concurrent preload requests for the same key.
    /// </summary>
    internal sealed class UnityAudioBank
    {
        private readonly Dictionary<string, AudioClip> _cache = new();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _handles = new();
        private readonly Dictionary<string, Task> _inFlight = new();

        /// <summary>
        /// Loads and caches the <see cref="AudioClip"/> identified by <paramref name="key"/>.
        /// If the key is already cached, it returns immediately.
        /// If a load for this key is already in progress, awaits that operation instead of
        /// starting a new one.
        /// If the token is cancelled before loading begins, or while awaiting an
        /// in-flight load for the same key, the method returns without caching.
        /// </summary>
        /// <param name="key">Addressables address string. Case-sensitive.</param>
        /// <param name="cancellationToken">Token to abort the load operation.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if Addressables fails to load the clip.
        /// </exception>
        public async Task PreloadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.ContainsKey(key)) return;
            if (cancellationToken.IsCancellationRequested) return;
            if (_inFlight.TryGetValue(key, out var existing))
            {
                await existing;
                // Caller may have cancelled while waiting on an in-flight load.
                // The original load continues; do not remove it from _inFlight.
                return;
            }

            var loadTask = LoadAsync(key, cancellationToken);
            _inFlight[key] = loadTask;

            try
            {
                await loadTask;
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        /// <summary>
        /// Releases the cached <see cref="AudioClip"/> identified by <paramref name="key"/>.
        /// Active handles playing that clip continue until natural completion.
        /// No-op if the key is not cached.
        /// </summary>
        /// <param name="key">Addressables address string. Case-sensitive.</param>
        public void Unload(string key)
        {
            if (!_handles.TryGetValue(key, out AsyncOperationHandle<AudioClip> handle)) return;

            Addressables.Release(handle);
            _handles.Remove(key);
            _cache.Remove(key);
        }

        /// <summary>
        /// Attempts to retrieve a cached <see cref="AudioClip"/> by key.
        /// Returns false if the key was not preloaded.
        /// </summary>
        /// <param name="key">Addressables address string. Case-sensitive.</param>
        /// <param name="clip">The cached clip, or null if not found.</param>
        public bool TryGet(string key, out AudioClip clip) => _cache.TryGetValue(key, out clip);

        private async Task LoadAsync(string key, CancellationToken cancellationToken)
        {
            var handle = Addressables.LoadAssetAsync<AudioClip>(key);
            await handle.Task;
            
            if (cancellationToken.IsCancellationRequested)
            {
                Addressables.Release(handle);
                return;
            }
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                throw new InvalidOperationException($"[AudioBank] Failed to load clip for key: '{key}'.");
            }

            _cache[key] = handle.Result;
            _handles[key] = handle;
        }
    }
}