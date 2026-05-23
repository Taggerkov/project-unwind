using System;
using Systems.Audio.Shared;

namespace Systems.Audio.Contracts
{
    /// <summary>
    /// An immutable descriptor for a single audio playback request.
    /// Pass with the <c>in</c> modifier to avoid struct copies at call sites.
    /// </summary>
    internal readonly struct AudioRequest
    {
        /// <summary>
        /// Constructs an <see cref="AudioRequest"/> from a <see cref="AudioEvent"/> asset.
        /// </summary>
        /// <param name="audioEvent">The sound event to construct the request from. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="audioEvent"/> is null.</exception>
        public static AudioRequest FromAudioEvent(AudioEvent audioEvent)
        {
            return audioEvent == null
                ? throw new ArgumentNullException(nameof(audioEvent))
                : new AudioRequest(audioEvent.Key, audioEvent.Category, audioEvent.Volume, audioEvent.Speed,
                    audioEvent.Loop);
        }

        /// <summary>The bank key identifying the clip to play.</summary>
        public string Key { get; }

        /// <summary>The logical category governing routing and volume.</summary>
        public AudioCategory Category { get; }

        /// <summary>Playback volume. Must be zero or greater. Values above 1 are backend-dependent. Defaults to 1.</summary>
        public float Volume { get; }

        /// <summary>Playback speed as a pitch multiplier. 1 is normal speed. Negative values reverse playback where supported by the backend.</summary>
        public float Speed { get; }

        /// <summary>When true, the clip loops until explicitly stopped.</summary>
        public bool Loop { get; }

        /// <summary>
        /// Constructs an <see cref="AudioRequest"/> with the given playback parameters.
        /// </summary>
        /// <param name="key">Addressables asset GUID. Must not be null or whitespace.</param>
        /// <param name="category">Logical audio category for routing and bulk control.</param>
        /// <param name="volume">Playback volume. Defaults to 1. Clamping is the backend's responsibility.</param>
        /// <param name="speed">Playback speed multiplier. 1 is normal speed. Negative values reverse playback where supported. Defaults to 1.</param>
        /// <param name="loop">When true, the clip loops until explicitly stopped. Defaults to false.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        private AudioRequest(string key, AudioCategory category, float volume = 1f, float speed = 1f, bool loop = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Audio key must not be null or whitespace.", nameof(key));
            Key = key;
            Category = category;
            Volume = volume;
            Speed = speed;
            Loop = loop;
        }
    }
}