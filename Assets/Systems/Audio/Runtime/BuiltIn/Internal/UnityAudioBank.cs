using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Systems.Audio.Shared;

namespace Systems.Audio.Runtime.BuiltIn.Internal
{
    /// <summary>
    /// Resolves string keys to <see cref="AudioClip"/> assets via Addressables.
    /// Owns the preload cache and all Addressables handle lifetimes.
    /// Deduplicates concurrent preload requests for the same key, and warns when a clip is
    /// requested while already loaded or loading — clips are expected to be preloaded by a single owner.
    /// </summary>
    internal sealed class UnityAudioBank : IDisposable
    {
        /// <summary>Loaded clips keyed by their Addressables address string.</summary>
        private readonly Dictionary<string, AudioClip> _cache = new();

        /// <summary>Active Addressables operation handles keyed by address, used to release clips on unload.</summary>
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _handles = new();

        /// <summary>
        /// In-flight loads keyed by address, used to deduplicate concurrent preload requests for the same key.
        /// A <see cref="UniTaskCompletionSource"/> is used rather than the load <see cref="UniTask"/> itself
        /// because it supports multiple concurrent awaiters; awaiting a single shared task twice throws.
        /// </summary>
        private readonly Dictionary<string, UniTaskCompletionSource> _inFlight = new();

        /// <summary>Keys for which <see cref="Unload"/> was called while a load was in-flight.</summary>
        private readonly HashSet<string> _pendingUnloads = new();

        /// <summary>True after <see cref="Dispose"/> has been called; prevents post-disposal writes to the cache.</summary>
        private bool _disposed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Number of clips currently held in the preload cache. Development builds only.</summary>
        internal int CacheCount => _cache.Count;

        /// <summary>Number of loads currently in progress. Development builds only.</summary>
        internal int InFlightCount => _inFlight.Count;
#endif

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
        public async UniTask PreloadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.ContainsKey(key))
            {
                WarnRedundantRequest(key);
                return;
            }
            if (cancellationToken.IsCancellationRequested) return;
            if (_inFlight.TryGetValue(key, out var existing))
            {
                WarnRedundantRequest(key);
                // A load for this key is already running; await its shared completion.
                // The original load owns cancellation; this caller's token does not abort it.
                await existing.Task;
                return;
            }

            var completion = new UniTaskCompletionSource();
            _inFlight[key] = completion;

            try
            {
                await LoadAsync(key, cancellationToken);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
                throw;
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        /// <summary>
        /// Releases the cached <see cref="AudioClip"/> identified by <paramref name="key"/>.
        /// Active handles playing that clip continue until natural completion.
        /// If a load for this key is in-flight, the release is deferred until the load completes.
        /// No-op if the key is neither cached nor in-flight.
        /// </summary>
        /// <param name="key">Addressables address string. Case-sensitive.</param>
        public void Unload(string key)
        {
            if (_handles.TryGetValue(key, out var handle))
            {
                Addressables.Release(handle);
                _handles.Remove(key);
                _cache.Remove(key);
                return;
            }

            if (_inFlight.ContainsKey(key))
                _pendingUnloads.Add(key);
        }

        /// <summary>
        /// Releases all cached clips and clears internal state.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _inFlight.Clear();
            foreach (var handle in _handles.Values) Addressables.Release(handle);
            _handles.Clear();
            _cache.Clear();
            _pendingUnloads.Clear();
        }

        /// <summary>
        /// Attempts to retrieve a cached <see cref="AudioClip"/> by key.
        /// Returns false if the key was not preloaded.
        /// </summary>
        /// <param name="key">Addressables address string. Case-sensitive.</param>
        /// <param name="clip">The cached clip, or null if not found.</param>
        public bool TryGet(string key, out AudioClip clip) => _cache.TryGetValue(key, out clip);

        /// <summary>
        /// Warns that <paramref name="key"/> was requested while already loaded or loading.
        /// Clips are expected to be preloaded from a single owner; a redundant request is a wasted call
        /// and risks one owner unloading a clip another still depends on.
        /// Marked Conditional so both the call and the clip-name lookup are stripped from release builds.
        /// </summary>
        /// <param name="key">Addressables address string of the redundantly requested clip.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnRedundantRequest(string key)
        {
            var name = _cache.TryGetValue(key, out var clip) && clip != null ? clip.name : key;
            AudioDiagnostics.Warn(
                $"'{name}' was requested but is already loaded or loading. " +
                "Preload each clip from a single owner; redundant preloads waste calls and risk premature unload.");
        }

        /// <summary>
        /// Performs the Addressables load for <paramref name="key"/>, caches the result, and stores the handle.
        /// Releases the Addressables handle without caching if the bank was disposed, the token was cancelled, or the load failed.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if Addressables reports a failed load status.</exception>
        private async UniTask LoadAsync(string key, CancellationToken cancellationToken)
        {
            var handle = Addressables.LoadAssetAsync<AudioClip>(key);
            await handle.Task;

            if (_disposed || cancellationToken.IsCancellationRequested || _pendingUnloads.Remove(key))
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