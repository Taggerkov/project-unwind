using UnityEngine;

namespace Systems.Audio.Shared
{
    /// <summary>
    /// The available audio backends. Determines which <see cref="Contracts.IAudioService"/> implementation
    /// is constructed at runtime.
    /// </summary>
    public enum AudioBackend
    {
        /// <summary>Unity <see cref="AudioSource"/> implementation.</summary>
        BuiltIn,

        /// <summary>FMOD Studio implementation. Requires the FMOD Studio Unity package.</summary>
        FMOD
    }

    /// <summary>
    /// Configuration asset for the audio system.
    /// Create via right-click: Create → Audio → Settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "Unwind/Audio/Settings", order = 1)]
    public sealed class AudioSettings : ScriptableObject
    {
        /// <summary>The backend constructed by <see cref="AudioManager"/> at initialisation.</summary>
        [SerializeField]
        [Tooltip("The audio backend to use for playback. BuiltIn uses Unity AudioSource. FMOD requires the FMOD Studio package.")]
        private AudioBackend backend = AudioBackend.BuiltIn;

        /// <summary>
        /// Initial number of <see cref="AudioSource"/> components allocated in the pool.
        /// The pool resizes automatically if needed, but a higher initial capacity reduces runtime allocations.
        /// </summary>
        [SerializeField]
        [Tooltip("Initial number of AudioSources allocated in the pool. The pool resizes automatically if needed.")]
        private int poolSize = 24;

        /// <summary>Gets the backend constructed by <see cref="AudioManager"/> at initialisation.</summary>
        public AudioBackend Backend => backend;

        /// <summary>Gets the initial number of <see cref="AudioSource"/> components allocated in the pool.</summary>
        public int PoolSize => poolSize;
    }
}