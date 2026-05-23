using System;
using Systems.Audio.Contracts;

namespace Systems.Audio.Shared
{
    /// <summary>
    /// An immutable, point-in-time view of one active playback instance.
    /// Returned by <see cref="AudioManager.TryGetSnapshot"/> for read-only inspection by tooling or HUDs.
    /// </summary>
    public readonly struct AudioPlaybackSnapshot
    {
        /// <summary>Constructs a snapshot from the values captured at the call site.</summary>
        public AudioPlaybackSnapshot(Guid uuid, string name, AudioCategory category, bool isPlaying, bool isPaused,
            bool isLooping, float volume, float speed, float time, float length)
        {
            Uuid = uuid;
            Name = name;
            Category = category;
            IsPlaying = isPlaying;
            IsPaused = isPaused;
            IsLooping = isLooping;
            Volume = volume;
            Speed = speed;
            Time = time;
            Length = length;
        }

        /// <summary>The UUID assigned at playtime.</summary>
        public Guid Uuid { get; }

        /// <summary>The name of the AudioEvent that triggered this playback, or a short UUID fallback.</summary>
        public string Name { get; }

        /// <summary>The category this playback belongs to.</summary>
        public AudioCategory Category { get; }

        /// <summary>True when the playback is actively playing.</summary>
        public bool IsPlaying { get; }

        /// <summary>True when the playback is paused.</summary>
        public bool IsPaused { get; }

        /// <summary>True when the clip loops and must be stopped explicitly.</summary>
        public bool IsLooping { get; }

        /// <summary>The handle volume layer, excluding the category multiplier.</summary>
        public float Volume { get; }

        /// <summary>The handle speed layer, excluding the category multiplier.</summary>
        public float Speed { get; }

        /// <summary>The current playback position in seconds.</summary>
        public float Time { get; }

        /// <summary>The total clip length in seconds.</summary>
        public float Length { get; }
    }
}
