using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using Systems.Audio.Shared;

namespace Systems.Audio.Runtime.BuiltIn.Internal
{
    /// <summary>
    /// Manages a pool of <see cref="AudioSource"/> components for reuse across playback requests.
    /// Serves as the coroutine host for one-shot cleanup scheduling.
    /// </summary>
    internal sealed class UnityAudioPool : IDisposable
    {
        /// <summary>The Unity ObjectPool backing the AudioSource reuse strategy.</summary>
        private readonly ObjectPool<AudioSource> _pool;

        /// <summary>The MonoBehaviour host used to run one-shot cleanup coroutines on behalf of <see cref="AudioHandle"/>.</summary>
        private readonly MonoBehaviour _coroutineHost;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>The configured pre-warm size, used to detect runtime growth beyond it. Development builds only.</summary>
        private readonly int _initialSize;

        /// <summary>Total <see cref="AudioSource"/> instances created by the pool so far. Development builds only.</summary>
        private int _createdCount;

        /// <summary>True, once the overgrowth hint has been logged, so it fires at most once. Development builds only.</summary>
        private bool _grewWarned;

        /// <summary>Sources currently rented out for active playback. Development builds only.</summary>
        internal int ActiveCount => _pool.CountActive;

        /// <summary>Total sources created so far. Development builds only.</summary>
        internal int CreatedCount => _createdCount;

        /// <summary>The configured pre-warm size. Development builds only.</summary>
        internal int ConfiguredSize => _initialSize;

        /// <summary>True once the pool grew past its configured size. Development builds only.</summary>
        internal bool HasGrown => _grewWarned;
#endif

        /// <summary>
        /// Constructs the pool, creates the coroutine host <see cref="GameObject"/>, and pre-warms the pool.
        /// </summary>
        /// <param name="initialSize">Number of <see cref="AudioSource"/> instances to pre-allocate.</param>
        public UnityAudioPool(int initialSize)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _initialSize = initialSize;
#endif
            var host = new GameObject("[UnityAudioPool]");
            UnityEngine.Object.DontDestroyOnLoad(host);

            _coroutineHost = host.AddComponent<AudioPoolHost>();

            _pool = new ObjectPool<AudioSource>(
                createFunc: CreateSource,
                actionOnGet: null,
                actionOnRelease: ResetSource,
                actionOnDestroy: UnityEngine.Object.Destroy,
                collectionCheck: false,
                defaultCapacity: initialSize
            );

            for (var i = 0; i < initialSize; i++)
                _pool.Release(_pool.Get());
        }

        /// <summary>
        /// Creates a new pooled <see cref="AudioSource"/>. Warns once (development builds only) when creation
        /// exceeds the configured pre-warm size, signalling that <c>AudioSettings.PoolSize</c> is too low for
        /// the peak number of concurrent sounds and runtime allocations are occurring.
        /// </summary>
        private AudioSource CreateSource()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _createdCount++;
            if (_createdCount <= _initialSize || _grewWarned)
                return _coroutineHost.gameObject.AddComponent<AudioSource>();
            _grewWarned = true;
            AudioDiagnostics.Warn(
                $"AudioSource pool grew past its configured size ({_initialSize}). " +
                "Raise AudioSettings.PoolSize to cover peak concurrent sounds and avoid runtime allocations.");
#endif
            return _coroutineHost.gameObject.AddComponent<AudioSource>();
        }

        /// <summary>
        /// Rents an <see cref="AudioSource"/> from the pool.
        /// Caller is responsible for returning it via <see cref="Return"/>.
        /// </summary>
        public AudioSource Rent() => _pool.Get();

        /// <summary>
        /// Returns an <see cref="AudioSource"/> to the pool and resets its state.
        /// </summary>
        /// <param name="source">The source to return.</param>
        public void Return(AudioSource source) => _pool.Release(source);

        /// <summary>
        /// Starts a coroutine on the pool's host <see cref="GameObject"/>.
        /// Used by <see cref="AudioHandle"/> to schedule one-shot cleanup.
        /// </summary>
        /// <param name="routine">The coroutine to run.</param>
        /// <returns>A <see cref="Coroutine"/> reference that can be cancelled via <see cref="StopCoroutine"/>.</returns>
        public Coroutine StartCoroutine(IEnumerator routine)
            => _coroutineHost.StartCoroutine(routine);

        /// <summary>
        /// Stops a coroutine previously started via <see cref="StartCoroutine"/>.
        /// </summary>
        /// <param name="coroutine">The coroutine to stop.</param>
        public void StopCoroutine(Coroutine coroutine) => _coroutineHost.StopCoroutine(coroutine);

        /// <summary>
        /// Disposes the pool and destroys the host <see cref="GameObject"/>.
        /// </summary>
        public void Dispose()
        {
            _pool.Dispose();
            UnityEngine.Object.Destroy(_coroutineHost.gameObject);
        }

        /// <summary>Resets all stateful fields on a returned <see cref="AudioSource"/> so it is clean when re-rented.</summary>
        private static void ResetSource(AudioSource source)
        {
            source.clip = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = null;
        }

        /// <summary>
        /// Internal <see cref="MonoBehaviour"/> used solely as a coroutine host.
        /// Not accessible outside <see cref="UnityAudioPool"/>.
        /// </summary>
        private sealed class AudioPoolHost : MonoBehaviour
        {
        }
    }
}