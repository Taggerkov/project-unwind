using System;

namespace Systems.Audio.Contracts
{
    /// <summary>
    /// Represents a live audio playback instance.
    /// Obtained from <see cref="IAudioService.Play"/>.
    /// </summary>
    internal interface IAudioHandle
    {
        /// <summary>The assigned <see cref="Guid"/> at playtime.</summary>
        Guid Uuid { get; }
        
        /// <summary>Returns true while the audio is actively playing.</summary>
        bool IsPlaying { get; }

        /// <summary>Returns true while the audio is paused; false when playing, stopped, or released.</summary>
        bool IsPaused { get; }

        /// <summary>
        /// The handle's own volume layer, as last set via <see cref="SetVolume"/>.
        /// Excludes the category multiplier. Defaults to the request volume at playtime.
        /// </summary>
        float Volume { get; }

        /// <summary>
        /// The handle's own speed (pitch) layer, as last set via <see cref="SetSpeed"/>.
        /// Excludes the category multiplier. Defaults to the request speed at playtime.
        /// </summary>
        float Speed { get; }

        /// <summary>The category this handle plays into.</summary>
        AudioCategory Category { get; }

        /// <summary>True when the clip loops and the handle must be stopped explicitly.</summary>
        bool IsLooping { get; }

        /// <summary>The current playback position in seconds, or zero once released.</summary>
        float Time { get; }

        /// <summary>The total clip length in seconds.</summary>
        float Length { get; }

        /// <summary>
        /// Pauses playback, preserving the current position.
        /// Has no effect if the handle is not currently playing.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes playback from the position at which it was paused.
        /// Has no effect if the handle is not paused.
        /// </summary>
        void Resume();

        /// <summary>Stops playback immediately and releases the handle.</summary>
        void Stop();

        /// <summary>
        /// Overrides the playback volume at runtime.
        /// Must be zero or greater. Values above 1 are backend-dependent.
        /// </summary>
        /// <param name="volume">Target volume.</param>
        void SetVolume(float volume);

        /// <summary>
        /// Overrides the playback speed at runtime. Maps to pitch.
        /// 1 is normal speed. Negative values reverse playback where supported by the backend.
        /// </summary>
        /// <param name="speed">Target speed multiplier.</param>
        void SetSpeed(float speed);
        
        /// <summary>
        /// Raised when the handle is released, either via <see cref="Stop"/> or natural completion.
        /// The <see cref="Guid"/> is the UUID assigned by <see cref="AudioManager"/> at playtime.
        /// </summary>
        /// <remarks>
        /// Raised exactly once per playback instance.
        /// Active tracking is cleared synchronously by subscribers within the event; do not call
        /// playback methods on this UUID from within a handler.
        /// </remarks>
        event Action<Guid> OnReleased;
    }
}