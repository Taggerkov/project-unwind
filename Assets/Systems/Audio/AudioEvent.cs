using Systems.Audio.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Systems.Audio
{
    /// <summary>
    /// Defines a single audio playback configuration as a project asset.
    /// Drag into Inspector fields instead of using raw string keys.
    /// Create via right-click: Create → Audio → Sound Event.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioEvent", menuName = "Audio/Sound Event", order = 0)]
    public sealed class AudioEvent : ScriptableObject
    {
        /// <summary>The Addressables clip reference identifying the clip to play.</summary>
        [SerializeField] [Tooltip("Addressables clip reference. Drag an Addressable AudioClip asset here.")]
        private AssetReferenceT<AudioClip> clip;

        /// <summary>The logical category governing routing and volume.</summary>
        [SerializeField] [Tooltip("Logical audio category used for routing and bulk control.")]
        private AudioCategory category = AudioCategory.Sfx;

        /// <summary>Default playback volume. Must be zero or greater.</summary>
        [SerializeField]
        [Tooltip("Default playback volume. Must be zero or greater. Values above 1 are backend-dependent.")]
        private float volume = 1f;

        /// <summary>Default playback speed as a pitch multiplier. 1 is normal speed.</summary>
        [SerializeField]
        [Tooltip("Default playback speed. 1 is normal. Negative values reverse playback where supported.")]
        private float speed = 1f;

        /// <summary>When true, the clip loops until explicitly stopped.</summary>
        [SerializeField] [Tooltip("When enabled, the clip loops until Stop is called explicitly.")]
        private bool loop;

        /// <summary>The stable Addressables GUID identifying the clip asset.</summary>
        public string Key => clip.AssetGUID;

        /// <summary>The logical category governing routing and volume.</summary>
        public AudioCategory Category { get => category; set => category = value; }

        /// <summary>Default playback volume. Must be zero or greater.</summary>
        public float Volume { get => volume; set => volume = value; }

        /// <summary>Default playback speed as a pitch multiplier. 1 is normal speed.</summary>
        public float Speed { get => speed; set => speed = value; }

        /// <summary>When true, the clip loops until explicitly stopped.</summary>
        public bool Loop { get => loop; set => loop = value; }
    }
}