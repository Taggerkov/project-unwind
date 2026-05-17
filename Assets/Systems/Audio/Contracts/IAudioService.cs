using System;
using System.Threading;
using System.Threading.Tasks;

namespace Systems.Audio.Contracts
{
    /// <summary>
    /// The audio playback interface. Implemented by each backend.
    /// </summary>
    internal interface IAudioService
    {
        /// <summary>
        /// Loads and caches the clip identified by <paramref name="key"/> ahead of playback.
        /// Must be awaited before calling <see cref="Play"/> with the same key.
        /// </summary>
        /// <param name="key">Bank lookup key to preload.</param>
        /// <param name="cancellationToken">Token to cancel the load operation.</param>
        Task PreloadAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the cached clip identified by <paramref name="key"/> from memory.
        /// Any active handles playing that clip continue until they stop naturally.
        /// </summary>
        /// <param name="key">Bank lookup key to release.</param>
        void Unload(string key);

        /// <summary>
        /// Plays a clip described by the given request.
        /// </summary>
        /// <param name="request">The playback descriptor.</param>
        /// <param name="uuid">The <see cref="Guid"/> assigned by the caller to identify this playback instance.</param>
        /// <returns>A handle to the active playback instance.</returns>
        IAudioHandle Play(in AudioRequest request, Guid uuid);

        /// <summary>
        /// Pauses all currently playing handles in the given category.
        /// </summary>
        /// <param name="category">The category to pause.</param>
        void PauseAll(AudioCategory category);

        /// <summary>
        /// Resumes all paused handles in the given category.
        /// </summary>
        /// <param name="category">The category to resume.</param>
        void ResumeAll(AudioCategory category);

        /// <summary>Stops all currently playing sounds in the given category.</summary>
        /// <param name="category">The category to silence.</param>
        void StopAll(AudioCategory category);

        /// <summary>
        /// Sets the master volume for an entire category.
        /// Must be zero or greater. Values above 1 are backend-dependent.
        /// </summary>
        /// <param name="category">Target category.</param>
        /// <param name="volume">Normalised volume.</param>
        void SetCategoryVolume(AudioCategory category, float volume);

        /// <summary>
        /// Sets the master speed for an entire category.
        /// </summary>
        /// <param name="category">Target category.</param>
        /// <param name="speed">Target speed multiplier.</param>
        void SetCategorySpeed(AudioCategory category, float speed);
    }
}