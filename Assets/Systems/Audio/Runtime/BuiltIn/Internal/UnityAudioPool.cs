using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Systems.Audio.Runtime.BuiltIn.Internal
{
    /// <summary>
    /// Manages a pool of <see cref="AudioSource"/> components for reuse across playback requests.
    /// Serves as the coroutine host for one-shot cleanup scheduling.
    /// </summary>
    internal sealed class UnityAudioPool
    {
        private readonly ObjectPool<AudioSource> _pool;
        private readonly MonoBehaviour _coroutineHost;

        /// <param name="initialSize">Number of <see cref="AudioSource"/> instances to pre-allocate.</param>
        public UnityAudioPool(int initialSize)
        {
            var host = new GameObject("[UnityAudioPool]");
            Object.DontDestroyOnLoad(host);

            _coroutineHost = host.AddComponent<AudioPoolHost>();

            _pool = new ObjectPool<AudioSource>(
                createFunc: () => host.AddComponent<AudioSource>(),
                actionOnGet: null,
                actionOnRelease: ResetSource,
                actionOnDestroy: Object.Destroy,
                collectionCheck: false,
                defaultCapacity: initialSize
            );

            for (var i = 0; i < initialSize; i++)
                _pool.Release(_pool.Get());
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

        private static void ResetSource(AudioSource source)
        {
            source.clip = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
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